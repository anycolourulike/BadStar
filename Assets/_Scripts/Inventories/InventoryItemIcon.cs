using UnityEngine;
using UnityEngine.UI;
using Rambler.Combat;
using Rambler.Control;
using TMPro;

namespace Rambler.Inventories
{
    /// <summary>
    /// To be put on the icon representing an inventory item. The slot hands it the inventory and its slot index;
    /// Equip and Drop act on that exact slot. The inventory's GameObject is the character who owns it.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class InventoryItemIcon : MonoBehaviour
    {
        [SerializeField] Button equipBtn;
        [SerializeField] Button dropBtn;
        [SerializeField] TextMeshProUGUI itemNumber;

        //Kept public so existing prefab values still deserialise; set from the slot now
        public PlayerSelector playerSelector;
        public Inventory inventory;
        public GameObject player;
        public Weapon thisWeapon;
        int slotIndex = -1;

        public void SetItem(Inventory inventory, int slotIndex)
        {
            this.inventory = inventory;
            this.slotIndex = slotIndex;
            player = inventory != null ? inventory.gameObject : null;

            //The slot UI is rebuilt on every inventory change, but clear anyway so a reused icon never double-fires
            equipBtn.onClick.RemoveAllListeners();
            dropBtn.onClick.RemoveAllListeners();

            var iconImage = GetComponent<Image>();
            thisWeapon = inventory != null ? inventory.GetItemInSlot(slotIndex) : null;
            if (thisWeapon == null)
            {
                iconImage.enabled = false;
                itemNumber.text = "";
                return;
            }

            iconImage.enabled = true;
            iconImage.sprite = thisWeapon.GetIcon();
            itemNumber.text = inventory.GetNumberInSlot(slotIndex).ToString();
            equipBtn.onClick.AddListener(OnEquipBtnClick);
            dropBtn.onClick.AddListener(OnDropBtnClick);
        }

        public void OnEquipBtnClick()
        {
            if (thisWeapon == null || player == null) return;
            //Fighter handles medkits itself: heals, spends one, keeps the current weapon
            player.GetComponent<Fighter>().EquipWeapon(thisWeapon);
        }

        public void OnDropBtnClick()
        {
            if (thisWeapon == null || player == null) return;
            Weapon dropped = thisWeapon;
            int number = inventory.GetNumberInSlot(slotIndex);
            if (number <= 0) return;

            inventory.RemoveFromSlot(slotIndex, number);
            player.GetComponent<ItemDropper>().DropItem(dropped, number);

            //Only go empty-handed if the dropped item was the one in hand and none is left
            var fighter = player.GetComponent<Fighter>();
            if (fighter != null && fighter.weaponConfig == dropped && !inventory.HasItem(dropped))
            {
                fighter.EquipUnarmed();
            }
        }
    }
}
