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
        public Fighter fighter;
        public GameObject activePlayer;

        [Header("Prompt")]
        [SerializeField] Canvas prompt;         //World space button shown while the active player is in range
        [SerializeField] Button pickUpButton;

        int number = 1;
        int playerCollidersInside; //The player has more than one collider; hide only when all have left

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
            if (playerSelector == null) playerSelector = FindObjectOfType<PlayerSelector>();
            if (playerSelector != null) UpdatePlayer(playerSelector.activePlayer);

            if (prompt != null)
            {
                prompt.worldCamera = Camera.main; //World space canvases need an event camera to receive taps
                prompt.gameObject.SetActive(false);
            }
            if (pickUpButton != null)
            {
                pickUpButton.onClick.AddListener(PickUpItem);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject != activePlayer) return;
            playerCollidersInside++;
            ShowPrompt(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (other.gameObject != activePlayer) return;
            playerCollidersInside = Mathf.Max(0, playerCollidersInside - 1);
            if (playerCollidersInside == 0) ShowPrompt(false);
        }

        void ShowPrompt(bool show)
        {
            if (prompt != null) prompt.gameObject.SetActive(show);
        }

        public void Setup(Weapon item, int number)
        {
            this.weaponConfig = item;
            if (!item.IsStackable())
            {
                number = 1;
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
            if (playerSelector != null) UpdatePlayer(playerSelector.activePlayer);
            if (activePlayer == null || inventory == null) return;

            //Hayes and Rambler hold their own versions of each gun; use one only if this pickup has it set
            Weapon toAdd = weaponConfig;
            if (activePlayer.name == "Hayes" && hayesWeaponConfig != null) toAdd = hayesWeaponConfig;
            else if (activePlayer.name == "Rambler" && ramblerWeaponConfig != null) toAdd = ramblerWeaponConfig;
            if (toAdd == null)
            {
                Debug.LogWarning("WeaponPickUp " + name + " has no weapon set for " + activePlayer.name);
                return;
            }

            //Inventory raises inventoryUpdated, which redraws the inventory UI
            bool foundSlot = inventory.AddToFirstEmptySlot(toAdd, number);
            if (foundSlot)
            {
                weaponConfig = toAdd;
                if (fighter != null) fighter.weaponPickedUp = toAdd;
                this.gameObject.SetActive(false);
            }
        }

        void UpdatePlayer(GameObject newPlayer)
        {
            activePlayer = newPlayer;
            fighter = activePlayer != null ? activePlayer.GetComponent<Fighter>() : null;
            inventory = activePlayer != null ? activePlayer.GetComponent<Inventory>() : null;
            playerCollidersInside = 0;
            ShowPrompt(false);
        }
    }
}
