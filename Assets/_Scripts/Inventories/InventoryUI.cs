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
        //The inventory Redraw is currently listening to, so a switch can unhook the right one
        Inventory listeningTo;

        private void OnEnable()
        {
            Listen(playerInventory);
            Ui.color = Color.red;
        }

        private void OnDisable()
        {
            Ui.color = Color.white;
            Listen(null);
        }

        private void Start()
        {
            Redraw();
        }

        /// <summary>
        /// Show another character's inventory (PlayerSelector calls this on a character switch).
        /// </summary>
        public void SetInventory(GameObject newPlayer)
        {
            activePlayer = newPlayer;
            playerInventory = newPlayer != null ? newPlayer.GetComponent<Inventory>() : null;
            if (isActiveAndEnabled) Listen(playerInventory);
            Redraw();
        }

        void Listen(Inventory inventory)
        {
            if (listeningTo != null) listeningTo.inventoryUpdated -= Redraw;
            listeningTo = inventory;
            if (listeningTo != null) listeningTo.inventoryUpdated += Redraw;
        }

        public void Redraw()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            if (playerInventory == null) return;

            for (int i = 0; i < playerInventory.GetSize(); i++)
            {
                InventorySlotUI itemUI = Instantiate(InventoryItemPrefab, transform);
                itemUI.Setup(playerInventory, i);
            }
        }      
    }
}