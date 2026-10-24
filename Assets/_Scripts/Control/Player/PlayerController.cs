using System;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.AI;
using Rambler.Movement;
using Rambler.Combat;
using Rambler.Attributes;
using Rambler.Core;
using Cinemachine;
using Rambler.SceneManagement;
using System.Collections;
using UnityEngine.UI;

namespace Rambler.Control
{
    //[RequireComponent(typeof(NavMeshAgent))]
    public class PlayerController : MonoBehaviour
    {
        public Portal.DestinationIdentifier destinationID;
        public GameObject activePlayer;
        public Image  UIBorder;
        public Sprite playerImg;
        public Image shieldImg;
        [SerializeField] float maxNavMeshProjectionDistance = 1f;
        [SerializeField] LineRenderer lineRenderer;
        [SerializeField] GameObject ammoCounter;
        [SerializeField] GameObject interact;
        [SerializeField] PlayerVitals vitals;
        [SerializeField] GameObject shield;
        [SerializeField] Fighter fighter;
        public Vector3 target;
        public PlayerSelector playerSelector;
        public CinemachineVirtualCamera cineMachine;
        GameObject selectionUIObj;
        ActiveWeapon activeWeapon;
        Transform interactTarget;
        float holdDuration = 1f;               
        Transform handTransform;
        Animator rigController;        
        Animator anim;
       
        NavMeshAgent agent; 
        WeaponIK weaponIK;
        Mover mover;

        float doubleTapTimeThreshold = 0.25f;
        float lastTapTime;

        Transform targetStartPos;
        public bool isHolstered; 
        Vector3 pickUpDirection;
           
        GameObject weaponPU; 
        WeaponPickUp pickUp;
        Weapon weaponToEquip;
        float shakeTimer;        
        int interactions;
        bool isCrouching;
        bool shieldsUp;
        bool isDead;

        void Awake()
        {
            if (LevelManager.Instance != null)
            {
                var sceneRef = LevelManager.Instance.sceneRef;
                if (sceneRef == 0) return;
                if (sceneRef == 1) return;
                if (sceneRef == 2) return;
                if (sceneRef == 5) return;
            }
            Debug.Log("No Level Manager Found by PC");
        }

        void OnEnable()
        {
            if (UIBorder != null) UIBorder.color = Color.red;
            Health.playerDeath += PlayerDied;
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.positionCount = 0;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
        } 

        void OnDisable()
        {
            //Unsubscribe first; during scene unload the UI may already be destroyed
            Health.playerDeath -= PlayerDied;
            if (UIBorder != null) UIBorder.color = Color.white;
            if (lineRenderer != null) lineRenderer.enabled = false;
        } 
       
        void Start()
        { 
           if (interact != null)
           {
               interact.SetActive(false);
           }
           
           targetStartPos = interactTarget;
           rigController = GetComponent<Fighter>().rigController;
           handTransform = GetComponent<Fighter>().handTransform;
           
           agent = GetComponent<NavMeshAgent>();
           weaponIK = GetComponent<WeaponIK>();

           fighter = GetComponent<Fighter>();
           anim = GetComponent<Animator>();
           mover = GetComponent<Mover>();

           
        }

        void Update()
        { 
            if (shakeTimer > 0)
            { 
              shakeTimer -= Time.deltaTime;
              if(shakeTimer <= 0f) 
              {
                CinemachineBasicMultiChannelPerlin cineMachinePerlin = 
                cineMachine.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
                cineMachinePerlin.m_AmplitudeGain = 0f;
              }
            }            
            
            if (shieldsUp == true) 
            {
                ShieldsUp();
                vitals.SetEnergyBurnRate = 1f;
            } 
            else
            {              
                ShieldsDown();
                vitals.SetEnergyBurnRate = 0.1f; 
            }

            if (InteractWithCombat() || InteractWithMovement()) return;
        }      

        void LateUpdate()
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                if (!agent.isStopped)
                {
                    DisplayMovementLine();
                }
                else
                {
                    RemoveMovementLine();
                }
            }
            else
            {
                RemoveMovementLine();
            }
        }


        bool InteractWithCombat()
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, Mathf.Infinity))
                {
                    CombatTarget otherCombatTarget = hit.transform.GetComponent<CombatTarget>();
                    GameObject objTarget = hit.transform.gameObject;

                    Transform selectionUI = objTarget.transform.Find("SelectionIcon");
                    if ((selectionUI != null) && (objTarget.CompareTag("Enemy")))
                    {
                        selectionUIObj = selectionUI.gameObject;
                        selectionUIObj.SetActive(true);
                    }

                    var thisCombatTarget = GetComponent<CombatTarget>();
                    if (otherCombatTarget == thisCombatTarget) return false;
                    Fighter fighter = GetComponent<Fighter>();
                    fighter.CombatTarget = otherCombatTarget;
                    CapsuleCollider targetCapsule = hit.transform.GetComponent<CapsuleCollider>();
                    fighter.TargetCap = targetCapsule;

                    if (otherCombatTarget == null) return false; // continue;                
                    if (!GetComponent<Fighter>().CanAttack(otherCombatTarget.gameObject))
                    {
                        return false;
                    }
                    
                    fighter.Attack(otherCombatTarget.gameObject);

                    if ((objTarget.tag == "Enemy") || (!this.CompareTag("Dead")))
                    {
                        ShakeCamera(1.5f, 0.2f);
                    }

                    return true;
                }
            }
            return false;
            
        }
               

        bool InteractWithMovement()
        {          
           if(isDead == true) return false;
           fighter.CancelTarget();
           if (Input.touchCount == 0) return false; //No finger down, nothing to raycast
           bool hasHit = RaycastNavMesh(out target);
           
            if (Input.touchCount > 0 && (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)))
            {
                return false;
            }
            if (hasHit)
            {              
                if (Input.touchCount > 0 && Input.touchCount < 2)
                {
                    Touch touchInfo = Input.GetTouch(0);

                    if (touchInfo.phase == TouchPhase.Stationary)
                    {
                        var holdTime = holdDuration -= Time.deltaTime;
                        if (holdTime < .6f)
                        {                                                  
                            mover.StartMoveAction(target, 1f);                                                     
                            holdDuration = 1f;                           
                        }
                    }
                }

                if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
                {
                    holdDuration = 1f; //New touch, restart the hold-to-move timer
                    float timeSinceLastTap = Time.time - lastTapTime;
                    if (timeSinceLastTap < doubleTapTimeThreshold)
                    {
                        mover.StartMoveAction(target, 50f);
                        mover.FaceFlat(target);
                        anim.SetTrigger("Roll");
                    }
                    lastTapTime = Time.time;
                }
                return true;
            }
            return false;           
        }

        Transform GetHandTransform(Transform handTransform)
        {
            return this.handTransform;
        }    

        static Ray GetRay()
        {
            return Camera.main.ScreenPointToRay(Input.GetTouch(0).position);
        }

        bool RaycastNavMesh(out Vector3 target)
        {
            target = new Vector3();

            RaycastHit hit;
            bool hasHit = Physics.Raycast(GetRay(), out hit);
            if (!hasHit) return false;

            NavMeshHit navMeshHit;
            bool hasCastToNavMesh = NavMesh.SamplePosition(
            hit.point, out navMeshHit, maxNavMeshProjectionDistance, NavMesh.AllAreas);
            if (!hasCastToNavMesh) return false;

            target = navMeshHit.position;

            return true;
        }

        void DisplayMovementLine()
        {
            lineRenderer.enabled = true;
            lineRenderer.material.color = Color.black;
            lineRenderer.positionCount = agent.path.corners.Length;

            if (lineRenderer.positionCount > 0)
            {


                lineRenderer.SetPosition(0, transform.position);

                if (agent.path.corners.Length < 2)
                {
                    return;
                }

                for (int i = 1; i < agent.path.corners.Length; i++)
                {

                    if (i < lineRenderer.positionCount)
                    {
                        Vector3 pointPosition = new Vector3(agent.path.corners[i].x, agent.path.corners[i].y, agent.path.corners[i].z);
                        lineRenderer.SetPosition(i, pointPosition);
                    }
                    else
                    {
                        Debug.LogWarning("Index out of bounds: " + i);
                        break; // Exit the loop to avoid further errors
                    }

                }
            }
            else
            {
                Debug.LogWarning("LineRenderer has no positions. Make sure positionCount is greater than 0.");
            }
        }
        void RemoveMovementLine()
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }

        void OnTriggerEnter(Collider other)
        {            
            activePlayer = playerSelector.activePlayer;            
            if (this.gameObject != activePlayer) return;
            if (other.gameObject.CompareTag("weaponPU"))
            { 
               weaponPU = other.gameObject;
               pickUp = weaponPU.GetComponent<WeaponPickUp>();                                               
               interact.SetActive(true);
               interactions = 1;                            
            }

            if (other.gameObject.CompareTag("headPickUP"))
            {
               interactions = 2;
               weaponPU = other.gameObject;
               pickUp = weaponPU.GetComponent<WeaponPickUp>();
               interact.SetActive(true);
            }

            if (other.gameObject.CompareTag("UsePC"))
            {
                interactions = 3;
                weaponPU = other.gameObject;                                                              
                interact.SetActive(true);
            }

            if (other.gameObject.CompareTag("medKitPU"))
            {
                weaponPU = other.gameObject;
                pickUp = weaponPU.GetComponent<WeaponPickUp>();
                interact.SetActive(true);
                interactions = 4;
            }
        }

        public void InteractPressed()
        {
           agent.enabled = false;
           mover.enabled = false;
           Interact();
           interact.SetActive(false);
           pickUp = null;
        }

        public void Interact()
        {
            switch(interactions)
            {
                case 1:                
                      fighter.EquipUnarmed();  
                      anim.SetTrigger("pickUp");
                      pickUp.PickUpItem();
                      break;

                case 2:
                     fighter.SetLastWeapon = fighter.weaponConfig; 
                     fighter.EquipUnarmed();  
                     anim.SetTrigger("PicKUPHead");
                     pickUp.PickUpItem(); 
                     break;

                case 3:
                     interactTarget.position = weaponPU.transform.position;
                     fighter.SetLastWeapon = fighter.weaponConfig; 
                     fighter.EquipUnarmed();                
                     anim.SetTrigger("use");
                     DoorOpen.doorUnlocked();
                     //notify doors to open
                     //play SFX
                     //reassign target
                     break;

                case 4:
                    weaponToEquip = fighter.weaponConfig;
                    fighter.EquipUnarmed();
                    anim.SetTrigger("pickUp");
                    pickUp.PickUpItem();
                    Invoke("EquipWeaponAfterMedKitPU", 1.5f);
                    break;

            }            
        }

        void PlayerDied(GameObject player)
        {
            isDead = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (this.gameObject != activePlayer) return;
            if (other.gameObject.CompareTag("weaponPU"))
            { 
                pickUp = null;
                interact.SetActive(false);      
            }

            if (other.gameObject.CompareTag("medKitPU"))
            {
                pickUp = null;
                interact.SetActive(false);
            }

            if (other.gameObject.CompareTag("headPickUP"))
            { 
                pickUp = null;
                interact.SetActive(false);      
            }

            if(other.gameObject.CompareTag("UsePC"))
            { 
                pickUp = null;
                interact.SetActive(false);      
            }
        }

        public void ToggleCrouch()
        {
            isCrouching = !isCrouching;
            //Fire the trigger once per toggle, not every frame
            if (isCrouching)
            {
                Crouch();
            }
            else
            {
                StandUp();
            }
        }

        public void Crouch()
        {
            anim.SetTrigger("Crouch");
            fighter.RigWeightToZero();
        }

        public void StandUp()
        {
            //anim.SetTrigger("StandUp");
            //fighter.RigWeightToOne();
        }

        public void EquipWeaponAfterMedKitPU()
        {
            fighter.EquipWeapon(weaponToEquip);
        }

        public void EnableMover()
        {
            agent.enabled = true;
            mover.enabled = true;
        }

        public void HolsterWeapon()
        { 
            if(isHolstered == true)
            {
                return;
            }
            else 
            {  
               ammoCounter.SetActive(false);                                    
               isHolstered = true;
               weaponIK.AimTransform = null;          
               rigController.SetTrigger("holster_weapon"); 
               var fighter = GetComponent<Fighter>();
               fighter.RigWeightToZero();  
            }                                                
        }

        public void ActivateAmmoCounter()
       {         
          ammoCounter.SetActive(true);        
       }

        public void DeactivateAmmoCounter()
       {
            if(ammoCounter == true)
           {
               ammoCounter.SetActive(false);
           }  
       }

        public void ReloadActiveWeapon() 
        {
           anim.SetTrigger("Reload");
           activeWeapon = fighter.activeWeapon;
           activeWeapon.Reload();
        }
      
        public void AtWayPoint(String command)
        {
            string _command = command;
            anim.SetTrigger(command);
        }

        public void ToggleShields()
        { 
            if(shieldsUp == false)
            {
               AudioManager.PlayWeaponSound(weaponSFX: AudioManager.WeaponSound.ShieldUp, transform.position);
            }        
            shieldsUp = !shieldsUp;
        }

        void ShieldsUp()
        {
            shieldImg.color = Color.red;
            shield.SetActive(true);
            shieldsUp = true;
        }


        void ShieldsDown() 
        {
           shieldImg.color = Color.white;
           shield.SetActive(false);
           shieldsUp = false;
        } 

       void ShakeCamera(float intensity, float time)
       {
          if(fighter.activeWeapon.weaponType == ActiveWeapon.WeaponType.melee) return;
          CinemachineBasicMultiChannelPerlin cineMachinePerlin = 
          cineMachine.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

          cineMachinePerlin.m_AmplitudeGain = intensity;
          shakeTimer = time;
       }
    }
}