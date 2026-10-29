using UnityEngine;
using Rambler.Combat;

namespace Rambler.Ship
{
    //The shootable part of a ShipCamera. Weapons fire particle projectiles, so take their damage
    //the same way Health does and pass it on to the camera.
    public class ShipCameraHitbox : MonoBehaviour
    {
        [SerializeField] ShipCamera owner;

        void Awake()
        {
            if (owner == null) owner = GetComponentInParent<ShipCamera>();
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
