using System;
using Rambler.Movement;
using Rambler.Core;
using Rambler.Control;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections;
using Rambler.Inventories;
using Rambler.Utils;
using Rambler.Saving;
using Rambler.Attributes;
using UnityEngine.Animations;

namespace Rambler.Combat
{
    public class Fighter : MonoBehaviour, IAction, ISaveable
    {
        [SerializeField] AnimatorOverrideController animatorOverride;
        [SerializeField] float timeBetweenAttacks = 1.3f;
        [SerializeField] Weapon unarmed;
        [SerializeField] ParticleSystem punchImpact;
        [SerializeField] CapsuleCollider targetCapsule;

        public CapsuleCollider TargetCap { get { return targetCapsule; } set { targetCapsule = value; } }
        public ActiveWeapon activeWeapon;
        public Transform handTransform;
        public Animator rigController;
        public Weapon weaponPickedUp;
        public Weapon lastWeaponUsed;
        public Transform ramblerRig;
        public Weapon weaponConfig;

        public Weapon SetLastWeapon { set { lastWeaponUsed = value; } get { return lastWeaponUsed; } }
        float timeSinceLastAttack =
        Mathf.Infinity;

        Transform enemyPos;
        public Transform EnemyPos { get { return enemyPos; } set { enemyPos = value; } }
        public CombatTarget otherCombatTarget;
        public CombatTarget CombatTarget { get { return otherCombatTarget; } set { otherCombatTarget = value; } }
        public bool canAttack = false;
        [Tooltip("Keep attacking the current target after the finger lifts, until it is destroyed or the next tap (set when the player taps a target or uses the door panel's Shoot button).")]
        public bool HoldTarget;
        Rambler.Ship.ShipDoorDamage attackDoor;
        PlayerController playerController;
        Vector3 hitPointVector;
        GameObject weaponRef;
        Health targetHealth;
        Rambler.Ship.ShipDoorDamage targetDoor;
        AIController aiCon;
        Inventory inv;

        //Ammo each gun had when last put away, by weapon asset name, so switching guns keeps their counts (saved too)
        readonly System.Collections.Generic.Dictionary<string, AmmoRecord> ammoByWeapon =
            new System.Collections.Generic.Dictionary<string, AmmoRecord>();

        [System.Serializable]
        struct AmmoRecord
        {
            public int clip;
            public int reserve;
        }

        [System.Serializable]
        class SaveRecord
        {
            public string weapon;
            public System.Collections.Generic.List<string> ammoWeapons = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<int> ammoClips = new System.Collections.Generic.List<int>();
            public System.Collections.Generic.List<int> ammoReserves = new System.Collections.Generic.List<int>();
        }

        //Seconds between attacks for the current weapon: the Weapon asset's fireInterval, or timeBetweenAttacks
        public float AttackInterval => weaponConfig != null && weaponConfig.FireInterval > 0f ? weaponConfig.FireInterval : timeBetweenAttacks;

        WeaponIK weaponIk;
        FieldOfView FOV;
        Animator anim;
        Mover mover;
        CoverUser coverUser;
        EnemyAI enemyAI;
        CompanionBrain companionBrain;

        //Distance the current weapon can hit from (0 if none yet)
        public float AttackRange => activeWeapon != null ? activeWeapon.GetRange() : 0f;

        void OnEnable()
        {
            Health.targetDeath += OnTargetDeath;
            Health.playerDeath += StopMovement;
        }

        void OnDisable()
        {
            Health.targetDeath -= OnTargetDeath;
            Health.playerDeath -= StopMovement;
        }

        void Start()
        {
            //if kneeling play kneel trigger and disable ik
            inv = GetComponent<Inventory>();
            playerController = GetComponent<PlayerController>();
            FOV = GetComponent<FieldOfView>();
            aiCon = GetComponent<AIController>();
            mover = GetComponent<Mover>();
            rigController = GetComponent<Fighter>().rigController;
            weaponIk = GetComponent<WeaponIK>();
            anim = GetComponent<Animator>();
            coverUser = GetComponent<CoverUser>();
            enemyAI = GetComponent<EnemyAI>();
            companionBrain = GetComponent<CompanionBrain>();
            if (weaponConfig == null)
            {
                //Nothing set: the last weapon used, or bare hands
                if (lastWeaponUsed != null) EquipWeapon(lastWeaponUsed);
                else EquipUnarmed();
            }
            else
            {
                if(weaponConfig != unarmed)
                {
                    EquipWeapon(weaponConfig);
                    if (inv != null) inv.AddToFirstEmptySlot(weaponConfig, 1); //Only players carry an Inventory
                }
                else
                {
                    EquipUnarmed();
                }                                
            }
        }

        void Update()
        {
            timeSinceLastAttack += Time.deltaTime;
            if (enemyPos == null) { return; }
            if (this.gameObject.CompareTag("Enemy")) { enemyPos = TargetCap.transform; }
            if (enemyPos.CompareTag("AIConversant")) return;
            if (attackDoor != null && attackDoor.State == Rambler.Ship.ShipDoorDamage.DamageState.Destroyed) { CancelTarget(); return; } //Nothing left to hit
            if (HoldTarget && IsTargetDead()) { CancelTarget(); return; }
            if (coverUser != null && coverUser.IsMovingToCover) return; //Let it reach cover before chasing or shooting
            if (!GetIsInRange())
            {
                mover.MoveTo(enemyPos.transform.position, 1f);
            }
            else if(!this.gameObject.CompareTag("Player"))
            {
                mover.CancelNav();  //Trait Run & Gun Enemy Stands Still
                AttackBehaviour();
            }
            else
            {
                AttackBehaviour();
            }
        }

        void AttackBehaviour()
        {
            if (!this.gameObject.CompareTag("Player"))
            {                
                if (activeWeapon.curClip <= 0)
                {   
                    if (activeWeapon.IsAmmo == false)
                        //Play Out Of Ammo Text & Image
                        return;
                }
                //Hold fire without a clear view; whichever brain is running knows
                bool blind;
                if (enemyAI != null && enemyAI.isActiveAndEnabled) blind = !enemyAI.TargetVisible;
                else if (companionBrain != null && companionBrain.isActiveAndEnabled) blind = !companionBrain.TargetVisible;
                else blind = FOV != null && FOV.canSeePlayer == false;
                if (blind) return;
            }

            mover.FaceFlat(enemyPos.transform.position);
            AssignIKTarget();
            if (this.gameObject.CompareTag("Player") && enemyPos.gameObject.CompareTag("Companion")) return;
            if (this.gameObject.CompareTag("Companion") && enemyPos.gameObject.CompareTag("Player")) return;

            if (timeSinceLastAttack > AttackInterval)
            {
                if (HasProjectile())
                {
                    if (weaponConfig.isEMP == true)
                    {
                        anim.SetTrigger("EMPAttack");
                        timeSinceLastAttack = 0f;
                    }
                    else
                    {
                        if (activeWeapon.CurClip > 0)
                        {
                            Vector3 targetVector = GetEnemyLocation() + Vector3.up / 1.1f;
                            activeWeapon.LaunchProjectile(activeWeapon.MuzPos(), targetVector);
                            timeSinceLastAttack = 0f;

                            //Titles vary in case ("pistol", "Pistol Hayes" uses "Pistol"), so compare lower-case
                            switch (weaponConfig.weaponTitle != null ? weaponConfig.weaponTitle.ToLowerInvariant() : "")
                            {
                                case "pistol":
                                    AudioManager.PlayWeaponSound(weaponSFX: AudioManager.WeaponSound.pistolShoot, activeWeapon.transform.position);
                                    break;
                                case "smg":
                                    AudioManager.PlayWeaponSound(weaponSFX: AudioManager.WeaponSound.SMGShoot, activeWeapon.transform.position);
                                    break;
                                case "rifle":
                                    AudioManager.PlayWeaponSound(weaponSFX: AudioManager.WeaponSound.RifleShoot, activeWeapon.transform.position);
                                    break;
                                case "shotgun":
                                    AudioManager.PlayWeaponSound(weaponSFX: AudioManager.WeaponSound.ShotgunShoot, activeWeapon.transform.position);
                                    break;
                            }
                            return;
                        }
                    }
                }
                else
                {
                    mover.MoveTo(enemyPos.transform.position, 10);
                    MeleeAttack(target: enemyPos);
                    timeSinceLastAttack = 0f;
                }
            }
        }

        public void EquipWeapon(Weapon weapon)
        {
            if (weapon == null) return;
            //A medkit is used, not held: heal, spend one, and keep the current weapon in hand
            if (weapon.isMedKit == true)
            {
                var health = GetComponent<Health>();
                health.RestoreHealth();
                var inv = GetComponent<Inventory>();
                if (inv != null)
                {
                    int slot = inv.FindItemSlot(weapon);
                    if (slot >= 0) inv.RemoveFromSlot(slot, 1);
                }
                return;
            }

            RememberAmmo(); //The gun being put away keeps its counts for next time
            DestroyOldWeapon(handTransform: handTransform);
            weaponConfig = weapon;

            Spawn(handTransform: handTransform, animator: anim);
            if (activeWeapon == null) return; //Weapon prefab without an ActiveWeapon
            activeWeapon.SetRigController = rigController;
            //Enemies always get a fresh gun (unlimited reserve); everyone else gets back what this gun had
            AmmoRecord ammo = default;
            bool known = !CompareTag("Enemy") && ammoByWeapon.TryGetValue(weapon.name, out ammo);
            if (known) activeWeapon.Setup(weapon, gameObject, ammo.clip, ammo.reserve);
            else activeWeapon.Setup(weapon, gameObject, -1, 0);
            if (weaponConfig.isMelee)
            {
                mover.RigWeaponUnequipped();
                RigWeightToZero();
                rigController.Play("equip_" + weapon.weaponTitle);
                StartCoroutine(nameof(AimInit));
            }
            else
            {
                RigWeightToOne();
                rigController.Play("equip_" + weapon.weaponTitle);
                mover.RigWeaponEquipped();
                StartCoroutine(nameof(AimInit));
                if (this.gameObject.CompareTag("Player"))
                {
                    playerController.ActivateAmmoCounter();
                    activeWeapon.AmmoUIInit();
                }
            }
        }

        public void EquipUnarmed()
        {
            //Equip first: this spawns the empty-handed weapon and sets activeWeapon
            EquipWeapon(weapon: unarmed);
            RigWeightToZero();
            if (playerController != null) //AI fighters have no ammo HUD
            {
                playerController.DeactivateAmmoCounter();
                if (activeWeapon != null) activeWeapon.AmmoUIInit();
            }
        }

        public void EquipPickedUpWeapon()
        {
            //Don't set weaponConfig first: EquipWeapon files the old gun's ammo under weaponConfig's name
            if (weaponPickedUp == null || weaponPickedUp.isMedKit) return;
            EquipWeapon(weaponPickedUp);
        }

        public void PlayEquipedWeaponAnim()
        {
            rigController.Play("equip_" + weaponConfig.weaponTitle);
        }

        public void RigWeightToZero()
        {
            mover.RigWeightToZero();
        }

        public void RigWeightToOne()
        {
            mover.RigWeaponEquipped();
        }

        public bool CanAttack(GameObject combatTarget)
        {
            if (combatTarget == null) { return false; }
            var targetToTest = combatTarget.GetComponent<CapsuleCollider>();
            return targetToTest != null;
        }

        public void Attack(GameObject combatTarget)
        {
            GetComponent<ActionScheduler>().StartAction(this);
            if (combatTarget != null)
            {
                enemyPos = combatTarget.GetComponent<Transform>();
                attackDoor = combatTarget.GetComponentInParent<Rambler.Ship.ShipDoorDamage>();
            }
        }

        public void CancelTarget()
        {
            CombatTarget = null;
            TargetCap = null;
            enemyPos = null;
            attackDoor = null;
            HoldTarget = false;
        }

        //Someone died: a held target is kept unless it was the one that died
        void OnTargetDeath()
        {
            if (HoldTarget && !IsTargetDead()) return;
            CancelTarget();
        }

        bool IsTargetDead()
        {
            if (enemyPos == null) return true;
            var health = enemyPos.GetComponentInParent<Health>();
            return health != null && health.IsDead();
        }

        //Refills the current gun's reserve
        public void AssignAmmo()
        {
            if (activeWeapon != null) activeWeapon.FullAmmo();
        }

        void RememberAmmo()
        {
            if (activeWeapon == null || weaponConfig == null || weaponConfig.isMelee) return;
            ammoByWeapon[weaponConfig.name] = new AmmoRecord { clip = activeWeapon.CurClip, reserve = activeWeapon.Reserve };
        }

        public void CancelNav()
        {
            mover.CancelNav();
        }

        //Saves the weapon in hand and every gun's ammo
        public object CaptureState()
        {
            RememberAmmo();
            var record = new SaveRecord { weapon = weaponConfig != null ? weaponConfig.name : null };
            foreach (var pair in ammoByWeapon)
            {
                record.ammoWeapons.Add(pair.Key);
                record.ammoClips.Add(pair.Value.clip);
                record.ammoReserves.Add(pair.Value.reserve);
            }
            return record;
        }

        public void RestoreState(object state)
        {
            //Older saves stored just the weapon name
            string weaponName = state as string;
            if (state is SaveRecord record)
            {
                weaponName = record.weapon;
                ammoByWeapon.Clear();
                for (int i = 0; i < record.ammoWeapons.Count; i++)
                {
                    ammoByWeapon[record.ammoWeapons[i]] = new AmmoRecord { clip = record.ammoClips[i], reserve = record.ammoReserves[i] };
                }
            }
            if (string.IsNullOrEmpty(weaponName)) return;
            Weapon weapon = UnityEngine.Resources.Load<Weapon>(weaponName);
            if (weapon == null)
            {
                Debug.LogWarning(name + ": saved weapon " + weaponName + " not found in Resources; keeping the current one");
                return;
            }
            //Re-equipping would store the current gun's counts over the restored ones, so forget the hand first
            weaponConfig = null;
            EquipWeapon(weapon);
        }

        public void ReloadSeq()
        {
            CancelTarget();
            rigController.SetTrigger("Reload");
            activeWeapon.Reload();
        }

        public void ReloadFin()
        {
            Debug.Log("ReloadFinCalled");
            activeWeapon.IsAmmo = true;
        }

        public void SlowRollAnim()
        {
            anim.speed = 1;
        }

        Vector3 GetEnemyLocation()
        {
            return hitPointVector = CombatTarget.TargetFuturePos(activeWeapon.AimTransform().position);
        }

        void Spawn(Transform handTransform, Animator animator)
        {
            DestroyOldWeapon(handTransform: handTransform);

            if (weaponConfig != null)
            {
                weaponRef = Instantiate(weaponConfig.equippedPrefab, handTransform);
                ActiveWeaponInit();
            }
            if (animatorOverride != null)
            {
                animator.runtimeAnimatorController = animatorOverride;
            }
        }

        bool HasProjectile()
        {
            if (weaponConfig.projectile != false)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        IEnumerator AimInit()
        {
            yield return new WaitForSeconds(0.3f);
            var aimTransform = activeWeapon.AimTransform();
            weaponIk.AimTransform = aimTransform;
        }

        void DestroyOldWeapon(Transform handTransform)
        {
            var oldWeapon = FindChildWithTag();
            if (oldWeapon == null) return;
            Destroy(oldWeapon);
        }

        GameObject FindChildWithTag()
        {
            Transform hand = handTransform;
            foreach (Transform transform in hand)
            {
                if (transform.tag == "weapon")
                {
                    var childObj = transform.gameObject;
                    return childObj;
                }
            }
            return null;
        }

        void AssignIKTarget()
        {
            weaponIk.targetTransform.position = GetEnemyLocation() + (Vector3.up / 1.1f);
        }

        bool GetIsInRange()
        {
            if (enemyPos != null)
            {
                return Vector3.Distance(transform.position, enemyPos.position) < activeWeapon.GetRange();
            }
            return false;
        }

        void MeleeAttack(Transform target)
        {
            if (target == null) { return; }
            targetHealth = target.GetComponent<Health>();
            targetDoor = target.GetComponentInParent<Rambler.Ship.ShipDoorDamage>(); //Doors can be beaten down too
            anim.SetTrigger("meleeAttack");
        }

        void MeleeEvent()
        {
            if (punchImpact != null) Instantiate(punchImpact, handTransform.position, handTransform.rotation); //The effect is optional; the hit must still land
            AudioManager.PlayHumanSound(AudioManager.HumanSound.Hit2, this.transform.position);
            if (targetHealth != null) targetHealth.TakeDamage(50f);
            else if (targetDoor != null) targetDoor.TakeDamage(50f);
        }

        void EMPAttack()
        {
            if (HasProjectile())
            {
                activeWeapon.LaunchProjectile(handTransform, GetEnemyLocation());
            }
        }

        void EMPEnd()
        {

        }

        void EndMelee()
        {

        }

        void ActiveWeaponInit()
        {
            activeWeapon = weaponRef != null ? weaponRef.GetComponentInChildren<ActiveWeapon>() : null;
        }

        void StopMovement(GameObject player)
        {
            CancelNav();
        }
    }
}