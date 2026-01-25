using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rambler.Inventories;
using Rambler.Combat;

namespace Rambler.Inventories
{
    /// <summary>
    /// Handles spawning pickups when item dropped into the world.
    /// 
    /// Must be placed on the root canvas where items can be dragged. Will be
    /// called if dropped over empty space. 
    /// </summary>
    public class InventoryDropTarget : MonoBehaviour
    {
        public void AddItems(Weapon item)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            player.GetComponent<ItemDropper>().DropItem(item, 1);
        }

        public int MaxAcceptable(Weapon item)
        {
            return int.MaxValue;
        }       
    }
}