using Rambler.Core;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Rambler.Combat
{
    [CreateAssetMenu(fileName = "Weapon", menuName = "Weapons/new", order = 0)]
    public class Weapon : ScriptableObject, ISerializationCallbackReceiver
    {       
        [SerializeField] AnimatorOverrideController animatorOverride = null;  
        public ActiveWeapon activeWeapon;        
        public GameObject equippedPrefab;
        public Projectile projectile;
        public string weaponTitle;
        public bool isGrenadeLauncher;
        public bool isPistol;
        public bool isMedKit;
        public bool isRifle;        
        public bool isMelee;
        public bool isEMP;

        [Header("Stats (0 = the old default for this weapon type)")]
        [Tooltip("Seconds between shots (or swings). 0 = the character's Fighter timeBetweenAttacks.")]
        [Min(0f)] [SerializeField] float fireInterval = 0f;
        [Tooltip("Rounds per magazine. 0 = default for the weapon type (pistol 15, SMG 20, rifle 25, shotgun 5, launcher 5, plasma 7).")]
        [Min(0)] [SerializeField] int magazineSize = 0;
        [Tooltip("Reserve rounds carried when the gun is new. 0 = default for the weapon type (pistol 90, SMG 120, rifle 150, shotgun 50, launcher 20, plasma 50).")]
        [Min(0)] [SerializeField] int maxReserve = 0;

        public float FireInterval => fireInterval;
        public int MagazineSize => magazineSize;
        public int MaxReserve => maxReserve;

        [Header("Kick and bursts (0 = the default for this weapon type)")]
        [Tooltip("Shots per burst, picked between x and y each burst. 0 = default (pistol 2-3, SMG 4-6, rifle 3-5, shotgun 1).")]
        [SerializeField] Vector2Int burstShots = Vector2Int.zero;
        [Tooltip("Seconds between bursts while the shooter brings the muzzle back on target. 0 = default 0.35-0.8.")]
        [SerializeField] Vector2 burstPause = Vector2.zero;
        [Tooltip("Spread in degrees with the gun settled. 0 = default for the type.")]
        [Min(0f)] [SerializeField] float baseSpread = 0f;
        [Tooltip("Degrees of muzzle climb and spread added by each shot. 0 = default for the type.")]
        [Min(0f)] [SerializeField] float kickPerShot = 0f;

        //Defaults by weapon type, used where a field is left at 0
        string Kind => isMelee ? "melee" : isPistol ? "pistol" : isRifle ? "rifle" : weaponTitle != null ? weaponTitle.ToLowerInvariant() : "";
        public Vector2Int BurstShots => burstShots.y > 0 ? burstShots :
            Kind.Contains("pistol") ? new Vector2Int(2, 3) : Kind.Contains("smg") ? new Vector2Int(4, 6) :
            Kind.Contains("shotgun") || isGrenadeLauncher ? new Vector2Int(1, 1) : new Vector2Int(3, 5);
        public Vector2 BurstPause => burstPause.y > 0f ? burstPause : new Vector2(0.35f, 0.8f);
        public float BaseSpread => baseSpread > 0f ? baseSpread : Kind.Contains("rifle") ? 0.8f : Kind.Contains("pistol") ? 1.5f : 2f;
        public float KickPerShot => kickPerShot > 0f ? kickPerShot :
            Kind.Contains("pistol") ? 2.2f : Kind.Contains("smg") ? 1.3f : Kind.Contains("shotgun") ? 4f : 1.6f;

        // CONFIG DATA
        [Tooltip("Auto-generated UUID for saving/loading. Clear this field if you want to generate a new one.")]
        [SerializeField] string itemID = null;
        [Tooltip("Item name to be displayed in UI.")]
        [SerializeField] string displayName = null;
        [Tooltip("Item description to be displayed in UI.")]
        [SerializeField] [TextArea] string description = null;
        [Tooltip("The UI icon to represent this item in the inventory.")]
        [SerializeField] public Sprite icon = null;
        [Tooltip("Item In the World To Be PickedUp.")]
        [SerializeField] WeaponPickUp pickup = null;
        [Tooltip("If true, multiple items of this type can be stacked in the same inventory slot.")]
        [SerializeField] bool stackable = false;
        
        // STATE
        static Dictionary<string, Weapon> itemLookupCache;


        public static Weapon GetFromID(string itemID)
        {
            if (itemLookupCache == null)
            {
                itemLookupCache = new Dictionary<string, Weapon>();
                var itemList = Resources.LoadAll<Weapon>("");
                foreach (var item in itemList)
                {
                    if (itemLookupCache.ContainsKey(item.itemID))
                    {
                        Debug.LogError(string.Format("Looks like there's a duplicate InventorySystem ID for objects: {0} and {1}", itemLookupCache[item.itemID], item));
                        continue;
                    }
                    itemLookupCache[item.itemID] = item;
                }
            }
            if (itemID == null || !itemLookupCache.ContainsKey(itemID)) return null;
            return itemLookupCache[itemID];
        }   

        public WeaponPickUp SpawnPickUp(Vector3 position, int number)
        {
            var pickup = Instantiate(this.pickup);
            pickup.transform.position = position;
            pickup.Setup(this, number);
            return pickup;
        }     

        public Sprite GetIcon()
        {
            return icon;
        }

        public string GetItemID()
        {
            return itemID;
        }

        public bool IsStackable()
        {
            return stackable;
        }

        public string GetDisplayName()
        {
            return displayName;
        }

        public string GetDescription()
        {
            return description;
        } 

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            // Generate and save a new UUID if this is blank.
            if (string.IsNullOrWhiteSpace(itemID))
            {
                itemID = System.Guid.NewGuid().ToString();
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // Required by the ISerializationCallbackReceiver but we don't need
            // to do anything with it.
        }       
    }
}