using System.Collections;
using System.Collections.Generic;
using Rambler.Movement;
using Rambler.Control;
using Rambler.Attributes;
using Rambler.Core;
using UnityEngine;

namespace Rambler.Dialogue
{
    public class AIConversant : MonoBehaviour, IAction
    {
        [SerializeField] Dialogue dialogue = null;
        [SerializeField] string conversantName;   
        [SerializeField] PlayerConversant playerConversant; 
        [SerializeField] GameObject Player;
        [SerializeField] GameUI gameUI;
        [SerializeField] Mover mover;
        [SerializeField] GameObject panel;
        [Tooltip("Off: talks once, then the trigger switches off for good (the old behaviour). On: talks again each time the player walks back in.")]
        [SerializeField] bool repeatable = false;
        
        SphereCollider sphereCollider;
        bool talkedThisVisit; //Repeatable: stops a restart while the player is still standing in the trigger

        void Start()
        {
            sphereCollider = GetComponent<SphereCollider>();
        }
        

        // void Update()
        // {
        //     if(Input.GetMouseButtonDown(0))
        //     {
        //         Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        //         RaycastHit hit;
        //         if(Physics.Raycast(ray, out hit))
        //         {    
        //             if(hit.transform.gameObject.CompareTag("AIConversant"))  
        //             {                        
        //                 HandleRaycast();                                                
        //             }                                     
        //         }
        //     }
        // }         

        public bool HandleRaycast()
        {            
            if (dialogue == null)
            {
                return false;
            }

            Health health = GetComponent<Health>();
            if (health && health.IsDead()) return false;

            // if (Input.GetMouseButtonDown(0) && !GetIsInRange())
            // {                 
            //    mover.MoveTo(this.transform.position, 2f);  
            //    StartCoroutine(CheckDistance());                  
            // }   
            // else
            // {
            //    mover.Cancel();                      
            // } 
            if (playerConversant == null || playerConversant.IsActive()) return false;

            playerConversant.StartDialogue(this, dialogue);
            if (panel != null) panel.SetActive(false);
            if (gameUI != null) gameUI.PauseGame();
            if (repeatable) talkedThisVisit = true;
            else if (sphereCollider != null) sphereCollider.enabled = false;
            //ammo counter
            return true;
        }

        // IEnumerator CheckDistance()
        // {
        //     while(!GetIsInRange())
        //     {
        //         yield return new WaitForEndOfFrame(); 
        //     }
        //     StopCoroutine(CheckDistance());
        // }

        public string GetName()
        {
            return conversantName;
        }

        // private bool GetIsInRange()
        // {
        //     if(Vector3.Distance(transform.position, this.transform.position) < 3f)
        //     {
        //         return false;
        //     }
        //     else
        //     {
        //         return true;
        //     }
        // }

        void OnTriggerEnter(Collider other)
        {
            if(other.gameObject == Player && !talkedThisVisit)
            {
                HandleRaycast();
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.gameObject == Player) talkedThisVisit = false;
        }

        public void CancelNav()
        {
            throw new System.NotImplementedException();
        }
    }
}