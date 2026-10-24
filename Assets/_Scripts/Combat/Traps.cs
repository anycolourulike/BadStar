using UnityEngine;
using System.Collections.Generic;
using Rambler.Attributes;
using Rambler.Movement;

namespace Rambler.Combat
{
    // Trigger volume that damages (and optionally slows) characters standing in it.
    // Needs a Collider set to Is Trigger on the same GameObject.
    [RequireComponent(typeof(Collider))]
    public class Traps : MonoBehaviour
    {
        public enum TrapKind
        {
            AreaDamage = 0, // Damages every damageByNSeconds while inside
            Explosive = 1   // Damages everything inside once, then disables itself
        }

        [Header("Trap General Settings")]
        public TrapKind TrapType;

        public bool isActive = true;
        public bool affectPlayers = true;  // Player & Companion tags
        public bool affectEnemys = false;  // Enemy tag

        [Header("Area Damage Trap")]
        [Range(0f, 1f)] public float movSpeedFactor = 1.0f;
        public float damage = 10f;
        public float damageByNSeconds = 1.0f;

        [Header("VFX")]
        public bool useTargetTransform = false;
        public Transform vfxTransform;
        public Vector3 vfxPosOffset = Vector3.zero;
        public GameObject vfxPrefab;

        GameObject vfxInstance;
        float damageTimer;
        readonly List<GameObject> targets = new List<GameObject>();
        readonly Dictionary<Mover, float> originalSpeeds = new Dictionary<Mover, float>();

        void Start()
        {
            if (vfxPrefab != null)
            {
                vfxInstance = Instantiate(vfxPrefab, transform.position, Quaternion.identity);
                vfxInstance.SetActive(false);
            }
        }

        void Update()
        {
            if (!isActive) return;

            targets.RemoveAll(t => t == null || IsDead(t));
            if (targets.Count == 0) return;

            damageTimer += Time.deltaTime;
            if (damageTimer >= damageByNSeconds)
            {
                damageTimer = 0f;
                ApplyDamage();
            }
        }

        void OnDisable()
        {
            //Never leave anyone slowed if the trap is switched off or destroyed
            foreach (Mover mover in new List<Mover>(originalSpeeds.Keys))
            {
                RestoreSpeed(mover);
            }
            targets.Clear();
        }

        void ApplyDamage()
        {
            //Copy so targets dying mid-loop can't modify the list being iterated
            foreach (GameObject target in new List<GameObject>(targets))
            {
                if (target == null) continue;

                DealDamage(target);
                if (vfxInstance != null) PlayVFX(target.transform);
            }

            if (TrapType == TrapKind.Explosive)
            {
                isActive = false;
                GetComponent<Collider>().enabled = false;
                gameObject.SetActive(false);
            }
        }

        void DealDamage(GameObject target)
        {
            Health health = target.GetComponentInParent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
                return;
            }

            THealth tHealth = target.GetComponentInParent<THealth>();
            if (tHealth != null) tHealth.TakeDamage(damage);
        }

        bool IsDead(GameObject target)
        {
            Health health = target.GetComponentInParent<Health>();
            if (health != null) return health.IsDead();

            THealth tHealth = target.GetComponentInParent<THealth>();
            return tHealth != null && tHealth.IsDead();
        }

        void PlayVFX(Transform targetPos)
        {
            if (useTargetTransform)
            {
                vfxInstance.transform.position = targetPos.position;
            }
            else
            {
                Transform source = vfxTransform != null ? vfxTransform : transform;
                vfxInstance.transform.position = source.position + vfxPosOffset;
                vfxInstance.transform.rotation = source.rotation;
            }

            vfxInstance.SetActive(true);
            foreach (ParticleSystem vfx in vfxInstance.GetComponentsInChildren<ParticleSystem>())
            {
                vfx.Play();
            }
        }

        void SlowTarget(GameObject target)
        {
            if (movSpeedFactor >= 1f) return;

            Mover mover = target.GetComponentInParent<Mover>();
            if (mover == null || originalSpeeds.ContainsKey(mover)) return;

            originalSpeeds[mover] = mover.maxSpeed;
            mover.maxSpeed *= movSpeedFactor;
        }

        void RestoreSpeed(Mover mover)
        {
            if (mover == null || !originalSpeeds.ContainsKey(mover)) return;

            mover.maxSpeed = originalSpeeds[mover];
            originalSpeeds.Remove(mover);
        }

        bool AffectsTarget(GameObject target)
        {
            if (affectPlayers && (target.CompareTag("Player") || target.CompareTag("Companion"))) return true;
            if (affectEnemys && target.CompareTag("Enemy")) return true;
            return false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!isActive) return;

            GameObject target = other.gameObject;
            if (!AffectsTarget(target) || targets.Contains(target)) return;

            //First target in starts the clock so damage lands shortly after entering
            if (targets.Count == 0) damageTimer = damageByNSeconds - 0.1f;

            targets.Add(target);
            SlowTarget(target);
        }

        void OnTriggerExit(Collider other)
        {
            GameObject target = other.gameObject;
            if (!targets.Remove(target)) return;

            RestoreSpeed(target.GetComponentInParent<Mover>());
        }
    }
}
