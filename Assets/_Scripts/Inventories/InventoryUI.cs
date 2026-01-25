using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rambler.Inventories;
using Rambler.Control;
using UnityEngine.UI;

namespace Rambler.UI.Inventories
{
    /// <summary>
    /// To be placed on the root of the inventory UI. Handles spawning all the
    /// inventory slot prefabs.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] InventorySlotUI InventoryItemPrefab = null;
        public Inventory playerInventory;
        public GameObject activePlayer;
        public GameObject playerCore;
        public PlayerSelector playerSelector;
        public Image Ui;

        private void Awake()
        {
            activePlayer = playerSelector.activePlayer;
            playerInventory = activePlayer.GetComponent<Inventory>();
            playerInventory = playerInventory.GetPlayerInventory();
        }
        private void OnEnable() 
        {
            playerInventory.inventoryUpdated += Redraw;
            Ui.color = Color.red;
        }

        private void OnDisable()
        {
            Ui.color = Color.white;
            playerInventory.inventoryUpdated -= Redraw;
        }

        private void Start()
        {  
            Redraw();     
        }

        void UpdatePlayer(GameObject newPlayer)
        {
            activePlayer = newPlayer;
           
            playerInventory = activePlayer.GetComponent<Inventory>();
            Redraw();
        }

        public void Redraw()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < playerInventory.GetSize(); i++)
            {
                InventorySlotUI itemUI = Instantiate(InventoryItemPrefab, transform);
                itemUI.Setup(playerInventory, i);
            }
        }      
    }
}