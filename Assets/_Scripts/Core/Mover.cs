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
            mainRig.weight = 0;
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
            if (!rb.isKinematic) rb.velocity = Vector3.zero; //Kinematic bodies have no velocity to clear
        }
               
        private void UpdateAnimator()
        {
            Animator anim = GetComponent<Animator>();
            Vector3 velocity = GetComponent<NavMeshAgent>().velocity;
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            float speed = localVelocity.z;
            anim.SetFloat("forwardSpeed", speed);            
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