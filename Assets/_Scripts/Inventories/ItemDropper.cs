using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Combat;
using Rambler.Saving;
using UnityEngine.SceneManagement;

namespace Rambler.Inventories
{
   /// <summary>
    /// To be placed on anything that wishes to drop pickups into the world.
    /// Tracks the drops for saving and restoring.
    /// </summary>
    public class ItemDropper : MonoBehaviour, ISaveable
    {
        // STATE
        private List<WeaponPickUp> droppedItems = new List<WeaponPickUp>();
        private List<DropRecord> otherSceneDroppedItems = new List<DropRecord>();

        // PUBLIC

        /// <summary>
        /// Create a pickup at the current position.
        /// </summary>
        /// <param name="item">The item type for the pickup.</param>
        public void DropItem(Weapon item, int number)
        {
            SpawnPickUp(item, GetDropLocation(), number);
        }

        // PROTECTED

        /// <summary>
        /// Override to set a custom method for locating a drop.
        /// </summary>
        /// <returns>The location the drop should be spawned.</returns>
        protected virtual Vector3 GetDropLocation()
        {
            return transform.position;
        }

        // PRIVATE
        public void SpawnPickUp(Weapon item, Vector3 spawnLocation, int number)
        {
            var pickup = item.SpawnPickUp(spawnLocation, number);
            droppedItems.Add(pickup);
        }

        [System.Serializable]
        private struct DropRecord
        {
            public string itemID;
            public SerializableVector3 position;
            public int number;
            public int sceneBuildIndex;
        }

        object ISaveable.CaptureState()
        {
            RemoveDestroyedDrops();
            var droppedItemsList = new List<DropRecord>();
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            foreach (WeaponPickUp pickup in droppedItems)
            {
                var droppedItem = new DropRecord();
                droppedItem.itemID = pickup.GetItem().GetItemID();
                droppedItem.position = new SerializableVector3(pickup.transform.position);
                droppedItem.number = pickup.GetNumber();
                droppedItem.sceneBuildIndex = buildIndex;
                droppedItemsList.Add(droppedItem);
            }
            droppedItemsList.AddRange(otherSceneDroppedItems);
            return droppedItemsList;
        }

        void ISaveable.RestoreState(object state)
        {
            var droppedItemsList = (List<DropRecord>)state;
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            otherSceneDroppedItems.Clear();
            foreach (var item in droppedItemsList)
            {
                if (item.sceneBuildIndex != buildIndex)
                {
                    otherSceneDroppedItems.Add(item);
                    continue;
                }
                var pickupItem = Weapon.GetFromID(item.itemID);
                Vector3 position = item.position.ToVector();
                int number = item.number;
                SpawnPickUp(pickupItem, position, number);
            }
        }

        /// <summary>
        /// Remove any drops in the world that have subsequently been picked up.
        /// </summary>
        private void RemoveDestroyedDrops()
        {
            var newList = new List<WeaponPickUp>();
            foreach (var item in droppedItems)
            {
                if (item != null)
                {
                    newList.Add(item);
                }
            }
            droppedItems = newList;
        }
    }    
}
