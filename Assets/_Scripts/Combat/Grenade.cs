using System.Collections.Generic;
using UnityEngine;
using Rambler.Attributes;

namespace Rambler.Combat
{
    //A thrown grenade: lobbed on an arc to a point, bounces, and goes off when its fuse runs out.
    //The blast hurts everyone in its radius that it can reach (walls shelter), cover doesn't help,
    //and damage falls off towards the edge.
    [RequireComponent(typeof(Rigidbody))]
    public class Grenade : MonoBehaviour
    {
        [SerializeField] float fuse = 1.8f;
        [SerializeField] float radius = 4.5f;
        [SerializeField] float damage = 140f;
        [Tooltip("Share of the damage still dealt at the edge of the blast.")]
        [Range(0f, 1f)] [SerializeField] float edgeDamage = 0.25f;
        [SerializeField] GameObject explosionFX;
        [Tooltip("What shelters characters from the blast.")]
        [SerializeField] LayerMask blastBlockers = (1 << 0) | (1 << 11);

        Rigidbody rb;
        float explodeAt = float.MaxValue;
        bool exploded;

        public float Fuse => fuse;
        public float Radius => radius;

        void Awake() => rb = GetComponent<Rigidbody>();

        //Throws it so it lands on target, from wherever it is now
        public void Launch(Vector3 target, GameObject thrower)
        {
            if (thrower != null)
            {
                var mine = GetComponent<Collider>();
                foreach (var c in thrower.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(mine, c);
            }
            Vector3 from = transform.position, to = target - from;
            float flat = new Vector2(to.x, to.z).magnitude;
            float time = Mathf.Clamp(flat / 11f, 0.55f, 1.3f);
            Vector3 velocity = to / time - 0.5f * Physics.gravity * time;
            rb.isKinematic = false;
            rb.velocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            explodeAt = Time.time + fuse;
        }

        void Update()
        {
            if (!exploded && Time.time >= explodeAt) Explode();
        }

        public void Explode()
        {
            exploded = true;
            Vector3 centre = transform.position + Vector3.up * 0.3f;
            if (explosionFX != null) Instantiate(explosionFX, transform.position, Quaternion.identity);

            var hurt = new HashSet<Health>();
            foreach (var col in Physics.OverlapSphere(centre, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var health = col.GetComponentInParent<Health>();
                if (health == null || health.IsDead() || !hurt.Add(health)) continue;
                Vector3 body = health.transform.position + Vector3.up * 1f;
                if (Physics.Linecast(centre, body, blastBlockers, QueryTriggerInteraction.Ignore)) continue; //Behind a wall
                float t = Mathf.Clamp01(Vector3.Distance(centre, body) / radius);
                health.TakeDamage(damage * Mathf.Lerp(1f, edgeDamage, t), ignoreCover: true);
            }
            foreach (var col in Physics.OverlapSphere(centre, radius, ~0, QueryTriggerInteraction.Collide))
            {
                var door = col.GetComponentInParent<Rambler.Ship.ShipDoorDamage>();
                if (door != null && col.GetComponent<Rambler.Ship.ShipDoorHitbox>() != null) door.TakeDamage(damage * 0.5f);
            }
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
