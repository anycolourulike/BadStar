using System.Collections;
using UnityEngine;
using Rambler.Attributes;
using Rambler.Movement;

namespace Rambler.Combat
{
    //Lets a character throw grenades: plays the throw, lets go of a Grenade from the right hand
    //part way through, and keeps count. Stands up out of a cover pose for the throw.
    public class GrenadeThrower : MonoBehaviour
    {
        [SerializeField] Grenade grenadePrefab;
        [SerializeField] int grenades = 2;
        [SerializeField] float minRange = 4f;
        [SerializeField] float maxRange = 16f;
        [Tooltip("Seconds between throws.")]
        [SerializeField] float cooldown = 6f;
        [Tooltip("Seconds into the throw animation that the grenade leaves the hand.")]
        [SerializeField] float releaseDelay = 0.65f;
        [Tooltip("Seconds after letting go before the character is ready again.")]
        [SerializeField] float recovery = 0.55f;
        [SerializeField] string throwTrigger = "ThrowGrenade";

        Animator anim;
        Fighter fighter;
        Mover mover;
        CoverUser cover;
        Health health;
        float readyAt;
        bool hasTrigger;
        Coroutine throwing;

        public int Count => grenades;
        public bool IsThrowing => throwing != null;
        public float Fuse => grenadePrefab != null ? grenadePrefab.Fuse : 1.8f;
        public float BlastRadius => grenadePrefab != null ? grenadePrefab.Radius : 4.5f;

        void Awake()
        {
            anim = GetComponent<Animator>();
            fighter = GetComponent<Fighter>();
            mover = GetComponent<Mover>();
            cover = GetComponent<CoverUser>();
            health = GetComponent<Health>();
            if (anim != null)
            {
                foreach (var p in anim.parameters) if (p.name == throwTrigger) hasTrigger = true;
            }
        }

        public bool CanThrowAt(Vector3 point)
        {
            if (grenadePrefab == null || grenades <= 0 || IsThrowing || Time.time < readyAt) return false;
            float d = Vector3.Distance(transform.position, point);
            return d >= minRange && d <= maxRange;
        }

        public bool Throw(Vector3 point)
        {
            if (!CanThrowAt(point)) return false;
            throwing = StartCoroutine(ThrowRoutine(point));
            return true;
        }

        public void Cancel()
        {
            if (throwing != null) StopCoroutine(throwing);
            throwing = null;
            Recover();
        }

        IEnumerator ThrowRoutine(Vector3 point)
        {
            grenades--;
            readyAt = Time.time + cooldown;
            if (cover != null) cover.PoseSuspended = true; //Cover poses are driven from Any State and would cut the throw short
            if (mover != null)
            {
                mover.CancelNav();
                mover.FaceFlat(point);
                mover.RigWeightToZero(); //Free the arms from the weapon IK
            }
            if (hasTrigger) anim.SetTrigger(throwTrigger);

            float release = Time.time + releaseDelay;
            while (Time.time < release)
            {
                if (mover != null) mover.FaceFlat(point);
                yield return null;
            }
            if (health == null || !health.IsDead())
            {
                Transform hand = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
                Vector3 from = hand != null ? hand.position : transform.position + Vector3.up * 1.6f + transform.forward * 0.4f;
                var grenade = Instantiate(grenadePrefab, from, Quaternion.identity);
                grenade.Launch(point, gameObject);
            }
            yield return new WaitForSeconds(recovery);
            throwing = null;
            Recover();
        }

        void Recover()
        {
            if (cover != null) cover.PoseSuspended = false;
            if (health != null && health.IsDead()) return;
            if (fighter != null && fighter.weaponConfig != null && !fighter.weaponConfig.isMelee) fighter.RigWeightToOne();
        }
    }
}
