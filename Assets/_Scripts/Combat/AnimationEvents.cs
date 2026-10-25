using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Control;
using Rambler.Attributes;
using Rambler.UI.Inventories;

namespace Rambler.Combat
{
    public class AnimationEvents : MonoBehaviour
    {        
        [SerializeField] PlayerController playerController;
        [SerializeField] CapsuleCollider capCol;
        [SerializeField] AIController aICon;
        [SerializeField] Fighter fighter;
        [SerializeField] InventoryUI invUI;
        public Transform playerHand;
        public Transform head;

        //Holstering was removed; the holster and shotgun clips still fire this event, so keep a receiver
        public void EquipUnarmed()
        {
        }

        public void EndPickup() 
        {
            fighter.EquipPickedUpWeapon();
            playerController.EnableMover();
            fighter.SetLastWeapon = fighter.weaponConfig;
            invUI.Redraw();
        }

        public void HeadAttach()
        {
           head.transform.SetParent(playerHand);
        }

        public void ReloadFinished()
        {
            Debug.Log("ReloadFinished");
            fighter.ReloadFin();
        }   
        
        public void DisableCapCol()
        {
            capCol.enabled = false;
        }

        public void EnableCapCol()
        {
            capCol.enabled = true;
        }
    }
}