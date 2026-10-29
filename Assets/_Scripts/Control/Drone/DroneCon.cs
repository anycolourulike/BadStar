using UnityEngine.AI;
using UnityEngine.UI;
using Rambler.Combat;
using Rambler.Core;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Movement;
using Rambler.Attributes;
using System;

namespace Rambler.Control
{

    public class DroneCon : MonoBehaviour
    {
        [SerializeField] GameObject closestTarget = null;
        [SerializeField] GameObject deathFX;
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
        public PlayerSelector playerSelector;
        public bool isAttacking;
        public bool isPatroling;
        public bool isDead;
        public Light light;
        public float targetDamage;
        SphereCollider sphereCol;
        StateMachine stateMachine;
        CapsuleCollider capsuleCol;
        CombatTarget combatTarget;
        public CombatTarget otherCombatTarget;
        DroneFOV FOV;
        public GameObject player;
        NavMeshAgent nav;
        
        Health health;
        Animator animator;
        Vector3 nextPosition;
        int currentWaypointIndex;
        public bool isSuicide;
        public bool isRanged;
        public bool isHealth;
        bool targetSFXPlayed;

        float TimerForNextAttack;
        float coolDown;

        void Awake()
        {
           sphereCol = GetComponent<SphereCollider>();
           health = GetComponent<Health>();
           FOV = GetComponent<DroneFOV>();
           animator = GetComponent<Animator>();
           combatTarget = GetComponent<CombatTarget>();
           //Drones the ship deploys at runtime aren't wired up in the scene
           if (playerSelector == null)
           {
               var playerCore = GameObject.Find("/PlayerCore");
               if (playerCore != null) playerSelector = playerCore.GetComponent<PlayerSelector>();
           }

            
            stateMachine = new StateMachine();
            //States
            var patrol = new DPatrol(this, patrolPath, mover, waypointTolerence, waypointDwellTime, patrolSpeedFraction, timeSinceArrivedAtWaypoint, currentWaypointIndex, nextPosition);
            var attack = new DAttack(this, mover, FOV, otherCombatTarget);
            var dead = new DDead(this, mover);
            //Transitions declaration
            At(attack, patrol, HasTarget());
            At(dead, attack, IsDead());
            //Initial State
            stateMachine.SetState(patrol);
            //Transitions initialization
            void At(IState to, IState from, Func<bool> condition) => stateMachine.AddTransition(to, from, condition);

            Func<bool> HasTarget() => () => isAttacking == true;
            Func<bool> IsDead() => () => isDead == true;
        }

        void Update()
        {
            stateMachine.Tick();

            if(FOV.canSeePlayer == true)
            {
                light.color = Color.red;
                NotPatrolling();
                AttackingTrue();
            }
        }

        public void IsPatrolling() { isPatroling = true; }
        public void NotPatrolling() { isPatroling = false; }

        public void AttackingTrue() { isAttacking = true; }
        public void AttackingFalse() { isAttacking = false; }

        public void FaceWaypoint(Vector3 dest)
        {
            float rotSpeed = 5f;
            Vector3 direction = dest - transform.position;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotSpeed * Time.deltaTime);
        }

        public void FacePlayer()
        {            
            if (playerSelector == null || playerSelector.activePlayer == null) return;
            player = playerSelector.activePlayer;
            mover.RotateTowards(player.transform);
        }

        public void OnTriggerEnter(Collider other)
        {
            if(other.gameObject.CompareTag("Player") || (other.gameObject.CompareTag("Companion")))
            {
                AudioManager.PlayTimbertoesSound(AudioManager.TimbertoesSound.droneExplosion, this.transform.position);
                if (deathFX != null)
                {
                    deathFX.SetActive(true);
                    deathFX.transform.SetParent(null);
                    deathFX.transform.position = other.transform.position + new Vector3(0, 1.3f, 0);
                }
                LeaveEnemyList();
                Destroy(gameObject);
                var health = other.GetComponent<Health>();
                if (health != null) health.TakeDamage(targetDamage);
            }
        }

        public void AttackTarget()
        {
            FacePlayer();
            mover.MoveTo(otherCombatTarget.transform.position, 25f);
            if (targetSFXPlayed == false)
            {
                targetSFXPlayed = true;
                AudioManager.PlayTimbertoesSound(AudioManager.TimbertoesSound.droneTarget, this.transform.position);
            }
        }

        public void DroneDeath()
        {
            AudioManager.PlayTimbertoesSound(AudioManager.TimbertoesSound.droneExplosion, this.transform.position);
            if (deathFX != null)
            {
                deathFX.SetActive(true);
                deathFX.transform.SetParent(null);
            }
            LeaveEnemyList();
            Destroy(gameObject);
        }

        //A drone destroys itself rather than dying through Health, so tell the spawner it's gone
        void LeaveEnemyList()
        {
            var spawner = FindObjectOfType<EnemySpawner>();
            if (spawner != null) spawner.RemoveEnemy(gameObject);
        }

        public void IncreaseSpeed()
        {
            mover.maxSpeed = 20f;
        }

        public void TargetHealthCheck()
        {
            if (closestTarget == null) return;
            var targetIsDead = closestTarget.GetComponent<Health>().isDead;
            if (targetIsDead == true) { UpdateTarget(); };
        }

        void UpdateTarget()
        {
            StartCoroutine("RefreshEnemiesList");
        }

        IEnumerator RefreshEnemiesList()
        {
            targetList.Clear();
            AssignTargetList();
            FindNearestTarget();
            yield return new WaitForSeconds(0.01f);
        }

        public void AssignTarget()
        {
            otherCombatTarget = FOV.targetObjs[0].GetComponent<CombatTarget>();
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
