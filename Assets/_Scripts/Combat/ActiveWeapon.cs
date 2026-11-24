using UnityEngine;
using Rambler.Core;
using TMPro;

namespace Rambler.Combat
{
    //The gun in a character's hand (spawned from Weapon.equippedPrefab). Holds the magazine and reserve, fires
    //projectiles, reloads, and drives the player's ammo HUD. Fighter calls Setup straight after spawning it with the
    //weapon's stats and the ammo this gun had last time it was held.
    public class ActiveWeapon : MonoBehaviour
    {
        public enum WeaponType {melee, pistol, smg, shotgun, rifle, plasma, launcher};
        public WeaponType weaponType;
        [SerializeField] Transform muzzleTransform;
        public GameObject ammoCountObj;
        [SerializeField] Transform aimTransform;
        [SerializeField] GameObject MuzzleFlash;
        [SerializeField] Projectile projectile;
        [SerializeField] float weaponRange;
        public TextMeshProUGUI totalAmmoDisplay;
        public TextMeshProUGUI magDisplay;
        Animator rigController;
        public Animator SetRigController { get{return rigController;} set{rigController = value;} }
        bool reloadAmmoSFX;
        bool outOfAmmoSFX;
        float weaponDamage;
        int magAmount;
        int totalAmmo;
        bool isAmmo = true;
        public bool IsAmmo { get {return isAmmo;} set {isAmmo = value;} }
        public int maxClip;
        public int curClip;
        public int CurClip { get {return curClip;} private set{ ;} }
        //Reserve rounds not yet in the magazine
        public int Reserve => totalAmmo;

        Weapon config;          //Stats source; null until Setup (then the per-type defaults apply)
        GameObject owner;       //Who fired, stamped on each projectile so it can't hit them or their side
        bool hasSetup;
        int shownClip = -1, shownReserve = -1;
        bool isNPC;

        void Awake()
        {
            isNPC = CompareTag("NPCWeapon");
        }

        void Start()
        {
            if (isNPC && !hasSetup) FullMag();
        }

        //Called by Fighter right after spawning. clip < 0 = a fresh gun (full magazine and full reserve).
        public void Setup(Weapon weapon, GameObject owner, int clip, int reserve)
        {
            config = weapon;
            this.owner = owner;
            hasSetup = true;
            magAmount = MagazineSize();
            maxClip = magAmount * 3;
            if (clip < 0)
            {
                curClip = magAmount;
                totalAmmo = ReserveSize();
            }
            else
            {
                curClip = Mathf.Clamp(clip, 0, magAmount);
                totalAmmo = Mathf.Max(0, reserve);
            }
            isAmmo = true;
            reloadAmmoSFX = false;
            outOfAmmoSFX = false;
            RefreshDisplays(force: true);
        }

        void Update()
        {
            if(weaponType == WeaponType.melee) return;
            if(!isNPC)
            {
               AmmoDisplay();
            }
            else if(curClip == 0)
            {
                isAmmo = false;
            }
        }

        //Binds the player's ammo HUD (found under PlayerCore) and shows this gun's counts
        public void AmmoUIInit()
        {
            GetProjectileDamage();
            var playerCore = GameObject.Find("PlayerCore");
            if (playerCore != null)
            {
                ammoCountObj = playerCore.transform.GetChild(0).GetChild(0).gameObject;
                var magazineCounter = ammoCountObj.transform.GetChild(0).gameObject;
                magazineCounter.SetActive(true);
                magDisplay = magazineCounter.GetComponentInChildren<TextMeshProUGUI>();
                var totalAmmoCounter = ammoCountObj.transform.GetChild(1).gameObject;
                totalAmmoCounter.SetActive(true);
                totalAmmoDisplay = totalAmmoCounter.GetComponentInChildren<TextMeshProUGUI>();
            }
            if (!hasSetup) FullMag(); //Older callers that never ran Setup still start with a full magazine

            if(isNPC) return;
            if(CompareTag("Companion")) return;
            RefreshDisplays(force: true);
        }

        //Full reserve (enemies, and the Timbertoes fighter)
        public void FullAmmo()
        {
            totalAmmo = ReserveSize();
        }

        public Transform AimTransform()
        {
            return aimTransform;
        }

        public Transform MuzPos()
        {
            return muzzleTransform;
        }

        public float GetRange()
        {
            return weaponRange;
        }

        /// <summary>Each shot fired, and each reload started (body animations listen, e.g. CoverKneelAim).</summary>
        public event System.Action Fired, Reloading;

        public void Reload()
        {
            if (magAmount <= 0) magAmount = MagazineSize();
            if(curClip >= magAmount)  return;
            if(isNPC)
            {
                //NPCs have unlimited reserve ammo
                ReloadAnimCheck();
                FullMag();
                return;
            }
            if(totalAmmo <= 0) return;

            ReloadAnimCheck();

            //Only move as many rounds as the reserve holds
            int moved = Mathf.Min(magAmount - curClip, totalAmmo);
            totalAmmo -= moved;
            curClip += moved;
            reloadAmmoSFX = false;
            RefreshDisplays();
        }

        //NPCs: a full magazine from their unlimited reserve, whichever weapon prefab they carry
        public void RefillMagazine()
        {
            FullMag();
            isAmmo = true;
        }

        public void LaunchProjectile(Transform muzzleTransform, Vector3 target)
        {
            if(curClip <= 0) return;
            ShootAnim();
            Projectile firedProjectile = Instantiate(projectile, muzzleTransform.position, muzzleTransform.rotation);
            if(weaponType == WeaponType.launcher)
            {
                firedProjectile.isGrenade = true;
            }
            if (owner == null)
            {
                var fighter = GetComponentInParent<Fighter>();
                if (fighter != null) owner = fighter.gameObject;
            }
            firedProjectile.Owner = owner;
            firedProjectile.DamageMultiplier = CombatModifiers.DamageOf(owner); //Barrels, ammo upgrades (1 for enemies)
            firedProjectile.SetTarget(target);
            if (MuzzleFlash != null)
            {
                GameObject muzzle = Instantiate(MuzzleFlash, muzzleTransform.position, muzzleTransform.rotation);
                muzzle.transform.parent = muzzleTransform.transform;
            }
            curClip--;
        }

        void AmmoDisplay()
        {
            RefreshDisplays();
            if(curClip == 0 && !outOfAmmoSFX)
            {
                outOfAmmoSFX = true;
                AudioManager.PlayWeaponSound(weaponSFX: AudioManager.WeaponSound.outOfAmmo, this.transform.position);
            }
        }

        //Only rewrites the HUD text when a number has changed (no string garbage every frame)
        void RefreshDisplays(bool force = false)
        {
            if (magDisplay != null && (force || curClip != shownClip))
            {
                shownClip = curClip;
                magDisplay.text = curClip.ToString();
                magDisplay.color = curClip == 0 ? Color.red : Color.white;
            }
            if (totalAmmoDisplay != null && (force || totalAmmo != shownReserve))
            {
                shownReserve = totalAmmo;
                totalAmmoDisplay.text = totalAmmo.ToString();
                totalAmmoDisplay.color = totalAmmo == 0 ? Color.red : Color.white;
            }
        }

        void GetProjectileDamage()
        {
            if(weaponType == WeaponType.melee || projectile == null)
            {
                weaponDamage = 50;
            }
            else
            {
              weaponDamage = projectile.GetDamage();
            }
        }

        void ReloadAnimCheck()
        {
            Reloading?.Invoke();
            if(reloadAmmoSFX == false)
            {
              AudioManager.PlayWeaponSound(weaponSFX: AudioManager.WeaponSound.weaponReload, this.transform.position);
              reloadAmmoSFX = true;
            }

            if(isNPC) return; //AI reload animation is played by the AI
            if (rigController != null)
            {
                switch(this.weaponType)
                {
                    case WeaponType.pistol:
                        rigController.SetTrigger("ReloadPistol");
                        break;
                    case WeaponType.smg:
                        rigController.SetTrigger("ReloadSMG");
                        break;
                    case WeaponType.rifle:
                        rigController.SetTrigger("ReloadRifle");
                        break;
                    //Shotgun, launcher and plasma have no reload animation yet
                }
            }
            outOfAmmoSFX = false;
        }

        void FullMag()
        {
            magAmount = MagazineSize();
            maxClip = magAmount * 3;
            curClip = magAmount;
            reloadAmmoSFX = false;
            if(isNPC) return;
            RefreshDisplays();
        }

        //Weapon asset value (or the old per-type default when it is 0), times any magazine upgrade on the owner
        int MagazineSize()
        {
            int size = BaseMagazineSize();
            if (size <= 0) return size;
            return Mathf.Max(1, Mathf.RoundToInt(size * CombatModifiers.MagazineOf(owner != null ? owner : GetComponentInParent<Fighter>()?.gameObject)));
        }

        int BaseMagazineSize()
        {
            if (config != null && config.MagazineSize > 0) return config.MagazineSize;
            switch (weaponType)
            {
                case WeaponType.pistol: return 15;
                case WeaponType.smg: return 20;
                case WeaponType.rifle: return 25;
                case WeaponType.shotgun: return 5;
                case WeaponType.launcher: return 5;
                case WeaponType.plasma: return 7;
                default: return 0; //Melee
            }
        }

        int ReserveSize()
        {
            if (config != null && config.MaxReserve > 0) return config.MaxReserve;
            switch (weaponType)
            {
                case WeaponType.pistol: return 90;
                case WeaponType.smg: return 120;
                case WeaponType.rifle: return 150;
                case WeaponType.shotgun: return 50;
                case WeaponType.launcher: return 20;
                case WeaponType.plasma: return 50;
                default: return 0;
            }
        }

        void ShootAnim()
        {
            Fired?.Invoke();
            if (rigController == null) return;
            if(isNPC)
            {
                rigController.SetTrigger("Shoot");
            }
            else
            {
               switch(this.weaponType)
               {
                case WeaponType.pistol:
                    rigController.SetTrigger("ShootPistol");
                    break;
                case WeaponType.smg:
                    rigController.SetTrigger("ShootSMG");
                    break;
                case WeaponType.rifle:
                    rigController.SetTrigger("ShootRifle");
                    break;
               }
            }
        }
    }
}
