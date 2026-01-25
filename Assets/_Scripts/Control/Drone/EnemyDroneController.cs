using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


public class EnemyDroneController : MonoBehaviour
    {
        Animator anim;
        NavMeshAgent agent;
        Transform mTransform;

        Rigidbody rb;
        Collider col;       


        public float speed;
        public int timesHit;
        public bool isDead;
        public bool isAggressive;        
        

        public Transform targetPlayer;
        public float detectionDistance = 1;

        public float ExplodeRange = 0.3f;
        public float minForce;
        public float maxForce;
        public float radius;

                     
        void Start()
        {
            mTransform = this.transform;
            agent = GetComponentInChildren<NavMeshAgent>();
            anim = GetComponentInChildren<Animator>();
            col = GetComponentInChildren<Collider>();
            rb = GetComponentInChildren<Rigidbody>();
           // targetPlayer = FindObjectOfType<Controller>().transform;
            
        }
        
        void Update()
        {
            if (isDead)
                return;

            HandlePlayerDetection();

            if (isAggressive)
            {
                agent.SetDestination(targetPlayer.position);
            }                     
            
        }

        void LateUpdate()
        {
            mTransform.position = agent.transform.position;
            agent.transform.localPosition = Vector3.zero;
            mTransform.rotation = agent.transform.rotation;
            agent.transform.localRotation = Quaternion.identity;
        }

        //public void OnHit(WeaponHook hook, Vector3 dir)
        //{

        //    if (!isDead)
        //    {
                
        //        if (timesHit < 7)
        //        {
        //            timesHit++;
                   
        //        }
        //        else
        //        {
        //            Dead();
        //            isDead = true;
        //        }
        //    }
        //}

        void Dead()
        {
            rb.isKinematic = false;
            rb.detectCollisions = false;            
            anim.SetBool("isdead", true);
            agent.enabled = false;        
          
        }    

       

        void HandlePlayerDetection()
        {
            if (targetPlayer == null)
                return;

            float distanceFromPlayer = Vector3.Distance(mTransform.position, targetPlayer.position);
                                   

            if (!isAggressive)
            {
                if (distanceFromPlayer < detectionDistance)
                {
                    isAggressive = true;
                    agent.SetDestination(targetPlayer.position);                    
                }
            }
            else
            {
                isAggressive = false;
                return;
            }

        }

}

