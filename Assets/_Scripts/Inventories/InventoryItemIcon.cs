using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Rambler.Inventories;
using Rambler.Combat;
using TMPro;
using Rambler.Control;
 
namespace Rambler.Inventories
{
    /// <summary>
    /// To be put on the icon representing an inventory item. Allows the slot to
    /// update the icon and number.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class InventoryItemIcon : MonoBehaviour
    {
        [SerializeField] Button equipBtn;
        [SerializeField] Button dropBtn;
        [SerializeField] TextMeshProUGUI itemNumber;
        
        public PlayerSelector playerSelector;
        public Inventory inventory;
        public GameObject player;  
        public Weapon thisWeapon;
        int slotIndex;
        int textInv;

        void OnEnable()
        {
            PlayerSelector.OnPlayerObjectChanged += UpdatePlayer;
        }

        void OnDisable()
        {
            PlayerSelector.OnPlayerObjectChanged -= UpdatePlayer;
        }

        void Start() 
        {
            var playerCore = GameObject.Find("PlayerCore");
            playerSelector = playerCore.GetComponent<PlayerSelector>();
            player = playerSelector.activePlayer;
            inventory = player.GetComponent<Inventory>();
        }
      
        public void SetItem(Weapon weaponConfig, int number)
        {            
            var iconImage = GetComponent<Image>();
            if (weaponConfig == null)
            {
                iconImage.enabled = false;
            }
            else
            {                
                iconImage.enabled = true;
                iconImage.sprite = weaponConfig.GetIcon();    
                equipBtn.onClick.AddListener(() => OnEquipBtnClick(weaponConfig));
                dropBtn.onClick.AddListener(() => OnDropBtnClick(weaponConfig, number));
                thisWeapon = weaponConfig;
                
                if(int.TryParse(itemNumber.text, out textInv))
                {
                    textInv += number;
                    itemNumber.text = textInv.ToString();
                }
                else
                {
                    textInv = number;
                    itemNumber.text = textInv.ToString();
                }                
            }
        }        
 
        public void OnEquipBtnClick(Weapon equipWeapon)
        {
            if(equipWeapon.isMedKit == true)
            {
                int i = 1;
                slotIndex = inventory.FindSlot(thisWeapon);
                inventory.RemoveFromSlot(slotIndex, i);
                itemNumber.text = i.ToString();
                //return;
            }
            player.GetComponent<Fighter>().EquipWeapon(equipWeapon);
        }
 
        public void OnDropBtnClick(Weapon dropWeapon, int number)
        {           
            player.GetComponent<ItemDropper>().DropItem(thisWeapon, number);  
            player.GetComponent<Fighter>().EquipUnarmed();

            slotIndex = inventory.FindSlot(thisWeapon);          
            inventory.RemoveFromSlot(slotIndex, number);
        }

        void UpdatePlayer(GameObject newPlayer)
        {
            Invoke(nameof(AssignActiveInv), 0.5f);
        }

        void AssignActiveInv()
        {
            player = playerSelector.activePlayer;
            inventory = player.GetComponent<Inventory>();
        }
    }
}