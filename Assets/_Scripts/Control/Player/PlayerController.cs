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
        Transform handTransform;
        Animator rigController;        
        Animator anim;
       
        NavMeshAgent agent; 
        WeaponIK weaponIK;
        Mover mover;

        float doubleTapTimeThreshold = 0.25f;
        float lastTapTime;

        Vector3 pickUpDirection;
           
        GameObject weaponPU; 
        WeaponPickUp pickUp;
        Weapon weaponToEquip;
        float shakeTimer;        
        int interactions;
        bool shieldsUp;
        bool shieldsShown = true; //Forces the first Update to apply the shield state
        bool isDead;

        void Awake()
        {
            //Only missing when a level is played without the Preloader and the editor's TestEnvironment is off
            if (LevelManager.Instance == null) Debug.Log("No Level Manager Found by PC");
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
               ShowInteract(false);
           }
           
           rigController = GetComponent<Fighter>().rigController;
           handTransform = GetComponent<Fighter>().handTransform;
           
           agent = GetComponent<NavMeshAgent>();
           weaponIK = GetComponent<WeaponIK>();

           fighter = GetComponent<Fighter>();
           anim = GetComponent<Animator>();
           mover = GetComponent<Mover>();

           //Characters made controllable at runtime (hacked robots) arrive without these wired up
           if (playerSelector == null) playerSelector = FindObjectOfType<PlayerSelector>();
           if (ammoCounter == null)
           {
               var hudAmmo = GameObject.Find("/PlayerCore/HUD/AmmoCounter");
               if (hudAmmo != null) ammoCounter = hudAmmo;
           }

           
        }

        void Update()
        {
            if (shakeTimer > 0)
            {
              shakeTimer -= Time.deltaTime;
              if (shakeTimer <= 0f) SetShake(0f);
            }

            //Only touch the shield and energy drain when the state changes, not every frame
            if (shieldsUp != shieldsShown)
            {
                if (shieldsUp) ShieldsUp(); else ShieldsDown();
                if (vitals != null) vitals.SetEnergyBurnRate = shieldsUp ? 1f : 0.1f;
                shieldsShown = shieldsUp;
            }

            if (HackAbility.IsArmed) { gesture = Gesture.None; return; } //The next tap picks a hack target instead
            HandlePointer();
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

        // ---------- Input ----------
        //One finger (or the mouse in the editor) at a time. What a press does is decided when it starts:
        //  on the HUD or the camera joystick - ignored by the world
        //  on a door                         - opens its info panel
        //  on an enemy                       - attack it (keeps firing after the finger lifts)
        //  anywhere else                     - double tap rolls there; hold to move, and keep holding to steer
        enum Gesture { None, Ignored, Combat, World }
        Gesture gesture;
        float gestureStart;
        float nextMoveOrder;

        //Seconds a press must be held before the player starts moving
        const float HoldToMove = 0.35f;
        //While held, the destination follows the finger this often
        const float SteerInterval = 0.2f;

        bool PointerState(out Vector2 position, out bool began, out bool held, out int fingerId)
        {
            position = default; began = false; held = false; fingerId = -1;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                position = touch.position;
                fingerId = touch.fingerId;
                began = touch.phase == TouchPhase.Began;
                held = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                return true;
            }
            //Mouse for testing in the editor (on phones Unity also reports touches as the mouse, so skip it there)
            if (!Application.isMobilePlatform && (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)))
            {
                position = Input.mousePosition;
                began = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                return true;
            }
            return false;
        }

        void HandlePointer()
        {
            if (isDead) return;
            bool down = PointerState(out Vector2 screenPos, out bool began, out bool held, out int fingerId);

            //A target picked by tap (or the door panel) lasts until the next press; anything else is dropped
            if (!down)
            {
                gesture = Gesture.None;
                if (!fighter.HoldTarget) fighter.CancelTarget();
                return;
            }

            if (began) StartGesture(screenPos, fingerId);
            if (!held) { gesture = Gesture.None; return; }

            if (gesture == Gesture.World && Time.time - gestureStart >= HoldToMove && Time.time >= nextMoveOrder)
            {
                //Steer towards wherever the finger is now; small wobbles don't matter
                if (RaycastNavMesh(screenPos, out target)) mover.StartMoveAction(target, 1f);
                nextMoveOrder = Time.time + SteerInterval;
            }
        }

        void StartGesture(Vector2 screenPos, int fingerId)
        {
            gestureStart = Time.time;
            nextMoveOrder = 0f;

            bool overUI = EventSystem.current != null &&
                          (fingerId >= 0 ? EventSystem.current.IsPointerOverGameObject(fingerId) : EventSystem.current.IsPointerOverGameObject());
            //Taps on the HUD (e.g. the door panel's buttons) never reach the world behind it, and
            //dragging the camera joystick never shoots or moves, even when the finger strays off it
            if (overUI || CameraJoystick.Active || Camera.main == null) { gesture = Gesture.Ignored; return; }

            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            var doorPanel = Rambler.Ship.DoorInfoPanel.Instance;
            //Tapping a door shows its info panel instead of shooting it (the panel has a Shoot button)
            var tappedDoor = doorPanel != null ? Rambler.Ship.DoorInfoPanel.Pick(ray) : null;
            if (tappedDoor != null)
            {
                doorPanel.Show(tappedDoor);
                gesture = Gesture.Ignored;
                return;
            }
            //A tap anywhere else in the world closes it
            if (doorPanel != null && doorPanel.IsOpen) doorPanel.Hide();

            if (TryAttack(ray)) { gesture = Gesture.Combat; return; }

            //Not an enemy: this press is about moving, and it replaces any target picked earlier
            fighter.CancelTarget();
            gesture = Gesture.World;
            if (RaycastNavMesh(screenPos, out target))
            {
                float timeSinceLastTap = Time.time - lastTapTime;
                if (timeSinceLastTap < doubleTapTimeThreshold)
                {
                    mover.StartMoveAction(target, 50f);
                    mover.FaceFlat(target);
                    anim.SetTrigger("Roll");
                }
                lastTapTime = Time.time;
            }
        }

        //Tapped an enemy: attack it, once per tap
        bool TryAttack(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity)) return false;
            GameObject objTarget = hit.transform.gameObject;
            CombatTarget otherCombatTarget = hit.transform.GetComponent<CombatTarget>();
            if (otherCombatTarget == null || otherCombatTarget == GetComponent<CombatTarget>()) return false;
            //Your own squad isn't a target: tapping them moves you as usual
            if (objTarget.CompareTag("Player") || objTarget.CompareTag("Companion")) return false;
            if (!fighter.CanAttack(otherCombatTarget.gameObject)) return false;

            Transform selectionUI = objTarget.transform.Find("SelectionIcon");
            if (selectionUI != null && objTarget.CompareTag("Enemy"))
            {
                selectionUIObj = selectionUI.gameObject;
                selectionUIObj.SetActive(true);
            }

            fighter.CombatTarget = otherCombatTarget;
            fighter.TargetCap = hit.transform.GetComponent<CapsuleCollider>();
            fighter.Attack(otherCombatTarget.gameObject);
            fighter.HoldTarget = true; //Keep firing after the finger lifts, until the target is destroyed or the next tap
            if (objTarget.CompareTag("Enemy")) ShakeCamera(1.5f, 0.2f);
            return true;
        }

        Transform GetHandTransform(Transform handTransform)
        {
            return this.handTransform;
        }    

        bool RaycastNavMesh(Vector2 screenPos, out Vector3 target)
        {
            target = new Vector3();
            if (Camera.main == null) return false;

            RaycastHit hit;
            bool hasHit = Physics.Raycast(Camera.main.ScreenPointToRay(screenPos), out hit);
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
            //Trigger messages reach disabled controllers too (companions), which may not have looked up the selector yet
            if (playerSelector == null) return;
            activePlayer = playerSelector.activePlayer;
            if (this.gameObject != activePlayer) return;
            //Weapon pick ups ("weaponPU") show their own world space prompt; see WeaponPickUp

            if (other.gameObject.CompareTag("headPickUP"))
            {
               interactions = 2;
               weaponPU = other.gameObject;
               pickUp = weaponPU.GetComponent<WeaponPickUp>();
               ShowInteract(true);
            }

            if (other.gameObject.CompareTag("UsePC"))
            {
                interactions = 3;
                weaponPU = other.gameObject;                                                              
                ShowInteract(true);
            }

            if (other.gameObject.CompareTag("medKitPU"))
            {
                weaponPU = other.gameObject;
                pickUp = weaponPU.GetComponent<WeaponPickUp>();
                ShowInteract(true);
                interactions = 4;
            }
        }

        void ShowInteract(bool show)
        {
            //The HUD interact button was removed; scenes may no longer assign it
            if (interact != null) interact.SetActive(show);
        }

        public void InteractPressed()
        {
           if (interactions == 0) return; //Nothing in reach
           agent.enabled = false;
           mover.enabled = false;
           //The animation's event normally turns movement back on; don't leave the player stuck if it never fires
           CancelInvoke(nameof(EnableMover));
           Invoke(nameof(EnableMover), 3f);
           Interact();
           interactions = 0;
           ShowInteract(false);
           pickUp = null;
        }

        public void Interact()
        {
            switch(interactions)
            {
                case 2:
                     if (pickUp == null) break;
                     fighter.SetLastWeapon = fighter.weaponConfig; 
                     fighter.EquipUnarmed();  
                     anim.SetTrigger("PicKUPHead");
                     pickUp.PickUpItem(); 
                     break;

                case 3:
                     if (weaponPU != null) mover.FaceFlat(weaponPU.transform.position);
                     fighter.SetLastWeapon = fighter.weaponConfig; 
                     fighter.EquipUnarmed();                
                     anim.SetTrigger("use");
                     DoorOpen.doorUnlocked();
                     //notify doors to open
                     //play SFX
                     //reassign target
                     break;

                case 4:
                    if (pickUp == null) break;
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
            if (other.gameObject.CompareTag("medKitPU"))
            {
                pickUp = null;
                ShowInteract(false);
            }

            if (other.gameObject.CompareTag("headPickUP"))
            { 
                pickUp = null;
                ShowInteract(false);      
            }

            if(other.gameObject.CompareTag("UsePC"))
            { 
                pickUp = null;
                ShowInteract(false);      
            }
        }

        public void EquipWeaponAfterMedKitPU()
        {
            fighter.EquipWeapon(weaponToEquip);
        }

        public void EnableMover()
        {
            CancelInvoke(nameof(EnableMover));
            agent.enabled = true;
            mover.enabled = true;
        }

        public void ActivateAmmoCounter()
       {         
          if (ammoCounter != null) ammoCounter.SetActive(true);        
       }

        public void DeactivateAmmoCounter()
       {
            if(ammoCounter == true)
           {
               if (ammoCounter != null) ammoCounter.SetActive(false);
           }  
       }

        public void ReloadActiveWeapon() 
        {
           activeWeapon = fighter != null ? fighter.activeWeapon : null;
           if (activeWeapon == null) return; //Bare hands
           anim.SetTrigger("Reload");
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
            if (shieldImg != null) shieldImg.color = Color.red;
            if (shield != null) shield.SetActive(true);
            shieldsUp = true;
        }


        void ShieldsDown() 
        {
           if (shieldImg != null) shieldImg.color = Color.white;
           if (shield != null) shield.SetActive(false);
           shieldsUp = false;
        } 

       void ShakeCamera(float intensity, float time)
       {
          if (fighter.activeWeapon == null || fighter.activeWeapon.weaponType == ActiveWeapon.WeaponType.melee) return;
          if (!SetShake(intensity)) return;
          shakeTimer = time;
       }

       //False when there's no camera with noise to shake (e.g. a hacked robot without one wired up)
       bool SetShake(float amplitude)
       {
          if (cineMachine == null) return false;
          var perlin = cineMachine.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
          if (perlin == null) return false;
          perlin.m_AmplitudeGain = amplitude;
          return true;
       }
    }
}