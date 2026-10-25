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
        PlayerController playerController;
        Vector3 hitPointVector;
        GameObject weaponRef;
        Health targetHealth;
        AIController aiCon;
        Inventory inv;

        WeaponIK weaponIk;
        FieldOfView FOV;
        Animator anim;
        Mover mover;

        void OnEnable()
        {
            Health.targetDeath += CancelTarget;
            Health.playerDeath += StopMovement;
        }

        void OnDisable()
        {
            Health.targetDeath -= CancelTarget;
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
            if (weaponConfig == null)
            {
                EquipWeapon(lastWeaponUsed);
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
            ActiveWeaponInit();
        }

        void Update()
        {
            timeSinceLastAttack += Time.deltaTime;
            if (enemyPos == null) { return; }
            if (this.gameObject.CompareTag("Enemy")) { enemyPos = TargetCap.transform; }
            if (enemyPos.CompareTag("AIConversant")) return;
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
                if (FOV.canSeePlayer == false) return;
            }

            mover.FaceFlat(enemyPos.transform.position);
            AssignIKTarget();
            if (this.gameObject.CompareTag("Player") && enemyPos.gameObject.CompareTag("Companion")) return;
            if (this.gameObject.CompareTag("Companion") && enemyPos.gameObject.CompareTag("Player")) return;

            if (timeSinceLastAttack > timeBetweenAttacks)
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
                        if (timeSinceLastAttack < 0.5f) return;
                        if (activeWeapon.CurClip > 0)
                        {
                            Vector3 targetVector = GetEnemyLocation() + Vector3.up / 1.1f;
                            activeWeapon.LaunchProjectile(activeWeapon.MuzPos(), targetVector);
                            timeSinceLastAttack = 0f;

                            switch (weaponConfig.weaponTitle)
                            {
                                case "pistol":
                                    AudioManager.PlayWeaponSound(weaponSFX: AudioManager.WeaponSound.pistolShoot, activeWeapon.transform.position);
                                    break;
                                case "SMG":
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
            if (weapon.isMedKit == true)
            {
                var health = GetComponent<Health>();
                health.RestoreHealth();
                var inv = GetComponent<Inventory>();
                var slot = inv.FindSlot(weapon);
                inv.RemoveFromSlot(slot, 1);
            }

            DestroyOldWeapon(handTransform: handTransform);
            weaponConfig = weapon;

            Spawn(handTransform: handTransform, animator: anim);
            activeWeapon.SetRigController = rigController;
            AssignAmmo();
            if (this.gameObject.CompareTag("Enemy"))
            {
                activeWeapon.FullAmmo();
            }
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
                activeWeapon.AmmoUIInit();
            }
        }

        public void EquipPickedUpWeapon()
        {
            weaponConfig = weaponPickedUp;
            if (weaponConfig.isMedKit) return;
            EquipWeapon(weaponConfig);
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
            }
        }

        public void CancelTarget()
        {
            CombatTarget = null;
            TargetCap = null;
            enemyPos = null;
        }

        public void AssignAmmo()
        {
            activeWeapon.FullAmmo();
        }

        public void CancelNav()
        {
            mover.CancelNav();
        }

        public object CaptureState()
        {
            Debug.Log("weaponSaved");
            return weaponConfig.name;
        }

        public void RestoreState(object state)
        {
            string weaponName = (string)state;
            Weapon weapon = UnityEngine.Resources.Load<Weapon>(weaponName);
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
            anim.SetTrigger("meleeAttack");
        }

        void MeleeEvent()
        {
            Instantiate(punchImpact, handTransform.position, handTransform.rotation);
            AudioManager.PlayHumanSound(AudioManager.HumanSound.Hit2, this.transform.position);
            targetHealth.TakeDamage(50f);
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
            activeWeapon = weaponRef.GetComponentInChildren<ActiveWeapon>();
        }

        void StopMovement(GameObject player)
        {
            CancelNav();
        }
    }
}