using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rambler.Control;
using Rambler.Inventories;
using UnityEngine.Events;
using System;
using UnityEngine.EventSystems;

namespace Rambler.Combat
{
   /// Set the vital data after creating the prefab.
   /// </summary>
   /// <param name="item">The type of item this prefab represents.</param>
   /// <param name="number">The number of items represented.</param>
   /// <summary/>
    public class WeaponPickUp : MonoBehaviour
    {
        public PlayerSelector playerSelector;
        public UnityAction PickUp; 
        public Weapon weaponConfig;
        public Weapon hayesWeaponConfig;
        public Weapon ramblerWeaponConfig;
        public Inventory inventory;   
        public GameObject interact;  
        public Fighter fighter;
        public GameObject activePlayer;
        int number = 1; 

        void OnEnable() 
        {            
            PickUp += PickUpItem;
            PlayerSelector.OnPlayerObjectChanged += UpdatePlayer;
        }   

        void OnDisable() 
        {
            PickUp -= PickUpItem;
            PlayerSelector.OnPlayerObjectChanged -= UpdatePlayer;
        }    

        void Start()
        {
            interact = GameObject.FindGameObjectWithTag("Interact"); 
            var playerCore = GameObject.Find("PlayerCore");
            playerSelector = playerCore.GetComponent<PlayerSelector>();
            activePlayer = playerSelector.activePlayer;
            inventory = activePlayer.GetComponent<Inventory>();
            fighter = activePlayer.GetComponent<Fighter>();         
        }

        public void Setup(Weapon item, int number)
        {
            this.weaponConfig = item;
            if (!item.IsStackable())
            {
                number = 0;
            }
            this.number = number;                        
        }  
    
        public Weapon GetItem()
        {
            return weaponConfig;
        }

        public int GetNumber()
        {
            return number;
        } 

        public bool CanBePickedUp()
        {
            return inventory.HasSpaceFor(weaponConfig);
        }  
       
        public void PickUpItem()
        {
            activePlayer = playerSelector.activePlayer;
            if (activePlayer.name == "Hayes")
            {
                weaponConfig = hayesWeaponConfig;
            }  
            else if(activePlayer.name == "Rambler")
            {
                weaponConfig = ramblerWeaponConfig;
            }

            bool foundSlot = inventory.AddToFirstEmptySlot(weaponConfig, number);
            if (foundSlot)
            {               
                fighter.weaponPickedUp = weaponConfig;                
                this.gameObject.SetActive(false);                                           
            } 
        }

        void UpdatePlayer(GameObject newPlayer)
        {
            activePlayer = newPlayer;
            fighter = activePlayer.GetComponent<Fighter>();
            inventory = activePlayer.GetComponent<Inventory>();
        }
    }        
}