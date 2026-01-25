using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Core;

namespace Rambler.Control
{
    public class TimbertoesFOV : MonoBehaviour
    {
        public float radius;
        [Range(0, 360)]
        public float angle;

        [SerializeField]
        public List<Collider> playerRefs = new List<Collider>();
        public List<GameObject> targetObjs = new List<GameObject>();
        public LayerMask targetMask;
        public LayerMask obstructionMask;
        public bool canSeePlayer;
        TimbertoesCon aIController;


        private void Start()
        {
            aIController = GetComponent<TimbertoesCon>();
            FindColliders();
            StartCoroutine(FOVRoutine());
        }

        void FindColliders()
        {
            if (this.gameObject.CompareTag("Enemy"))
            {
                targetObjs.AddRange(collection: GameObject.FindGameObjectsWithTag("Player"));
                foreach (var Target in targetObjs)
                {
                    var col = Target.GetComponent<CapsuleCollider>();
                    playerRefs.Add(col);
                }

            }
            else if (this.gameObject.CompareTag("Player"))
            {
                targetObjs.AddRange(collection: GameObject.FindGameObjectsWithTag("Enemy"));
                foreach (var Target in targetObjs)
                {
                    var col = Target.GetComponent<CapsuleCollider>();
                    playerRefs.Add(col);
                }
            }
        }

        private IEnumerator FOVRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(0.2f);

            while (true)
            {
                yield return wait;
                canSeePlayer = false;
                FieldOfViewCheck();
            }
        }


        private void FieldOfViewCheck()
        {
            Collider[] rangeChecks = Physics.OverlapSphere(transform.position, radius, targetMask);

            if (rangeChecks.Length != 0)
            {
                Transform target = rangeChecks[0].transform;
                Vector3 directionToTarget = (target.position - transform.position).normalized;

                if (Vector3.Angle(transform.forward, directionToTarget) < angle / 2)
                {
                    float distanceToTarget = Vector3.Distance(transform.position, target.position);

                    if (!Physics.Raycast(origin: transform.position,
                                         direction: directionToTarget,
                                         maxDistance: distanceToTarget,
                                         layerMask: obstructionMask))
                    {
                        canSeePlayer = true;
                        aIController.AssignTarget();
                        aIController.AttackingTrue();
                    }
                    else
                    {
                        canSeePlayer = false;
                        aIController.FindNearestTarget();
                    }
                }
                else
                    canSeePlayer = false;
                aIController.FindNearestTarget();

            }
            //else if(canSeePlayer)
            //aIController.FindNearestTarget();
            //canSeePlayer = false; 
        }

        // public bool PlayerDetect()
        // {         
        //   RaycastHit hit;
        //   if(Physics.Raycast(transform.position, this.transform.forward, out hit, 300f, playerLayer))
        //   {
        //     if(hit.transform.gameObject.CompareTag("Player"))
        //     {       
        //       return true;                  
        //     }    
        //     else
        //     {
        //       return false;
        //     }                
        //   } 
        //   return true;   
        // }      
    }
}
