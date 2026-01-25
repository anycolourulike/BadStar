using UnityEngine;
using Rambler.Combat;
using Rambler.Control;

namespace Rambler.Inventories
{
    /// <summary>
    /// To be placed at the root of a Pickup prefab. Contains the data about the
    /// pickup such as the type of item and the number.
    /// </summary>
    public class PickUp : MonoBehaviour
    {
        
        Weapon weaponConfig;
        int number = 1;
       

        
        Inventory inventory;

        // LIFECYCLE METHODS

        private void Awake()
        {
            GameObject playerCore = GameObject.Find("PlayerCore");
            if (playerCore != null)
            {
                PlayerSelector playerSelector = playerCore.GetComponent<PlayerSelector>();
                var player = playerSelector.activePlayer;
                inventory = player.GetComponent<Inventory>();
            }
            else
            {
                Debug.Log("PickUp Object" + " " + this.gameObject.name + " " + "cannot find PlayerCore");
            }
            
            
        }

        // PUBLIC

        /// <summary>
        /// Set the vital data after creating the prefab.
        /// </summary>
        /// <param name="item">The type of item this prefab represents.</param>
        /// <param name="number">The number of items represented.</param>
        public void Setup(Weapon weaponConfig, int number)
        {
            this.weaponConfig = weaponConfig;
            if (!weaponConfig.IsStackable())
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

        public void PickupItem()
        {
            bool foundSlot = inventory.AddToFirstEmptySlot(weaponConfig, number);
            if (foundSlot)
            {
                Destroy(gameObject);
            }
        }

        public bool CanBePickedUp()
        {
            return inventory.HasSpaceFor(weaponConfig);
        }
    }
}