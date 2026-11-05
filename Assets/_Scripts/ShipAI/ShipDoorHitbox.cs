using UnityEngine;
using Rambler.Combat;

namespace Rambler.Ship
{
    //The shootable part of a ShipDoorDamage. Weapons fire particle projectiles, so take their damage
    //the same way Health does and pass it on to the door.
    public class ShipDoorHitbox : MonoBehaviour
    {
        [SerializeField] ShipDoorDamage owner;

        void Awake()
        {
            if (owner == null) owner = GetComponentInParent<ShipDoorDamage>();
        }

        void OnParticleCollision(GameObject particleProj)
        {
            var proj = particleProj.GetComponent<Projectile>();
            if (proj == null || owner == null) return;
            owner.TakeDamage(proj.GetDamage());
            if (proj.HitEffect() != null) Instantiate(proj.HitEffect(), proj.GetAimLocation(), particleProj.transform.rotation);
            Destroy(proj.gameObject);
        }
    }
}
